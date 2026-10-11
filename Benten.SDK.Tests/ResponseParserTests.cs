using Benten.SDK;
using Benten.SDK.Internal;
using Benten.SDK.Models;
using Xunit;

namespace Benten.SDK.Tests;

/// <summary>
/// Parsing tests for Benten API responses. The JSON fixtures are taken from
/// API-REFERENCE.md and match what BentenAPI sends: Allow/Deny replies carry the
/// request ID as "_id"; error and NoResponse replies carry it as "RequestId".
/// </summary>
public class ResponseParserTests
{
    // ── Approval requests: user answers ─────────────────────────────────────

    [Fact]
    public void Allow_IsApproved()
    {
        var r = ResponseParser.ParseApproval(
            """{ "_id": "1aa78b2b-4652-492c-b5f4-847e1904c508", "Response": "Allow", "PhoneNumber": "6501111111", "Country": "United States" }""");

        Assert.Equal(ApprovalOutcome.Approved, r.Outcome);
        Assert.True(r.IsApproved);
        Assert.False(r.HasError);
        Assert.Equal("1aa78b2b-4652-492c-b5f4-847e1904c508", r.RequestId);
        Assert.Equal("", r.Error);
    }

    [Fact]
    public void Deny_IsDenied()
    {
        var r = ResponseParser.ParseApproval("""{ "_id": "abc", "Response": "Deny" }""");
        Assert.Equal(ApprovalOutcome.Denied, r.Outcome);
        Assert.True(r.IsDenied);
        Assert.False(r.HasError);
    }

    [Fact]
    public void NoResponse_IsTimedOut()
    {
        var r = ResponseParser.ParseApproval(
            """{ "RequestId": "5bdd322d-688d-4261-b47d-c6e5f9f3b907", "Country": "United States", "PhoneNumber": "6501111111", "Response": "NoResponse" }""");
        Assert.Equal(ApprovalOutcome.TimedOut, r.Outcome);
        Assert.True(r.IsTimedOut);
        Assert.False(r.HasError);
    }

    [Theory]
    [InlineData("allow", ApprovalOutcome.Approved)]
    [InlineData("DENY", ApprovalOutcome.Denied)]
    [InlineData("noresponse", ApprovalOutcome.TimedOut)]
    public void Answers_AreCaseInsensitive(string response, ApprovalOutcome expected)
    {
        var r = ResponseParser.ParseApproval($$"""{ "_id": "x", "Response": "{{response}}" }""");
        Assert.Equal(expected, r.Outcome);
    }

    // ── Approval requests: errors (the P0-1 bug) ────────────────────────────

    [Fact]
    public void StatusCodeInResponse_IsError()
    {
        // Login error example from API-REFERENCE.md
        var r = ResponseParser.ParseApproval("""
            {
              "RequestId": "8e8ec2e3-afb5-4b25-b118-e6134e687830",
              "Username": "alfie.noakes@benten.com",
              "Country": "United States",
              "PhoneNumber": "6501111111",
              "RequestType": "LoginRequest",
              "Response": "BSC4019"
            }
            """);

        Assert.Equal(ApprovalOutcome.Error, r.Outcome);
        Assert.True(r.HasError);
        Assert.False(r.IsApproved);
        Assert.Equal("BSC4019", r.ErrorCode);
        Assert.Equal("Invalid token.", r.ErrorMessage);
        Assert.Equal("BSC4019: Invalid token.", r.Error);
        Assert.Equal("8e8ec2e3-afb5-4b25-b118-e6134e687830", r.RequestId);
    }

    [Theory]
    [InlineData("BSC4021", "Client account is inactive. Contact Benten support.")]
    [InlineData("BSC4022", "No registered device found for the given country and phone number.")]
    [InlineData("BSC4056", "Device is not active. Activate the device first.")]
    [InlineData("BSC4060", "Invalid Benten credit card ID.")]
    [InlineData("BSC4062", "No IoT device found for the given name and MAC address.")]
    [InlineData("BSC4065", "Default Benten account unavailable. Contact Benten support.")]
    [InlineData("BSC4079", "Too many notifications sent to this phone. Try again in a few minutes.")]
    public void KnownStatusCodes_HaveMessages(string code, string message)
    {
        var r = ResponseParser.ParseApproval($$"""{ "_id": "x", "Response": "{{code}}" }""");
        Assert.Equal(ApprovalOutcome.Error, r.Outcome);
        Assert.Equal(code, r.ErrorCode);
        Assert.Equal(message, r.ErrorMessage);
    }

    [Fact]
    public void UnknownStatusCode_IsErrorWithGenericMessage()
    {
        var r = ResponseParser.ParseApproval("""{ "_id": "x", "Response": "BSC4999" }""");
        Assert.True(r.HasError);
        Assert.Equal("BSC4999", r.ErrorCode);
        Assert.Contains("BSC4999", r.ErrorMessage);
    }

    [Fact]
    public void ErrorField_IsError()
    {
        var r = ResponseParser.ParseApproval("""{ "_id": "x", "Error": "BSC4059" }""");
        Assert.True(r.HasError);
        Assert.Equal("BSC4059", r.ErrorCode);
    }

    [Fact]
    public void ErrorField_WinsOverAllow()
    {
        // Never approve if the server also reported an error.
        var r = ResponseParser.ParseApproval("""{ "_id": "x", "Response": "Allow", "Error": "BSC4019" }""");
        Assert.False(r.IsApproved);
        Assert.Equal("BSC4019", r.ErrorCode);
    }

    [Fact]
    public void PlainTextMessageInResponse_IsError()
    {
        var r = ResponseParser.ParseApproval("""{ "_id": "x", "Response": "One or more required field/s is missing" }""");
        Assert.True(r.HasError);
        Assert.Equal("", r.ErrorCode);
        Assert.Equal("One or more required field/s is missing", r.ErrorMessage);
        Assert.Equal("One or more required field/s is missing", r.Error);
    }

    [Fact]
    public void MessageContainingCode_ExtractsCode()
    {
        var r = ResponseParser.ParseApproval("""{ "_id": "x", "Response": "BSC4059 - System currently unavailable." }""");
        Assert.Equal("BSC4059", r.ErrorCode);
        Assert.Equal("BSC4059 - System currently unavailable.", r.Error);
    }

    [Fact]
    public void NonJsonBody_IsError()
    {
        var r = ResponseParser.ParseApproval("Message received is not a WebSocket request");
        Assert.True(r.HasError);
        Assert.Equal("Message received is not a WebSocket request", r.ErrorMessage);
        Assert.Equal("Message received is not a WebSocket request", r.RawResponse);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData((string?)null)]
    [InlineData("{}")]
    [InlineData("""{ "_id": "x" }""")]
    public void EmptyOrMissingResponse_IsError(string? raw)
    {
        var r = ResponseParser.ParseApproval(raw);
        Assert.Equal(ApprovalOutcome.Error, r.Outcome);
        Assert.False(r.IsApproved);
        Assert.NotEqual("", r.ErrorMessage);
    }

    [Fact]
    public void DoubleEncodedJson_IsUnwrapped()
    {
        var r = ResponseParser.ParseApproval("\"{ \\\"_id\\\": \\\"x\\\", \\\"Response\\\": \\\"Allow\\\" }\"");
        Assert.True(r.IsApproved);
        Assert.Equal("x", r.RequestId);
    }

    [Theory]
    [InlineData("""{ "_id": "id-1", "Response": "Allow" }""", "id-1")]                          // Allow/Deny reply
    [InlineData("""{ "RequestId": "id-2", "Response": "BSC4019" }""", "id-2")]                  // error reply
    [InlineData("""{ "RequestId": "id-3", "_id": "id-3", "Response": "Allow" }""", "id-3")]     // both (fixed server)
    [InlineData("""{ "RequestId": "", "_id": "id-4", "Response": "Allow" }""", "id-4")]         // empty RequestId falls back
    [InlineData("""{ "Response": "Allow" }""", "")]
    public void RequestId_IsReadFromRequestIdOrUnderscoreId(string raw, string expected)
    {
        Assert.Equal(expected, ResponseParser.ParseApproval(raw).RequestId);
    }

    [Fact]
    public void Requests_GetUniqueRequestIds()
    {
        var a = new LoginRequest();
        var b = new LoginRequest();

        Assert.False(string.IsNullOrWhiteSpace(a.RequestId));
        Assert.NotEqual(a.RequestId, b.RequestId);
    }

    [Fact]
    public void DefaultResult_IsNotApproved()
    {
        var r = new BentenResult();
        Assert.Equal(ApprovalOutcome.Error, r.Outcome);
        Assert.False(r.IsApproved);
    }

    // ── Access token (auth/token) ───────────────────────────────────────────

    [Fact]
    public void AccessToken_IsRead()
    {
        Assert.Equal(("eyJhbGci.x.y", 3600), ResponseParser.ParseAccessToken("""{ "Token": "eyJhbGci.x.y", "ExpiresIn": 3600 }"""));
        Assert.Equal(("eyJhbGci.x.y", 0), ResponseParser.ParseAccessToken("""{ "Token": "eyJhbGci.x.y" }"""));
        Assert.Equal(("eyJhbGci.x.y", 60), ResponseParser.ParseAccessToken("\"{ \\\"Token\\\": \\\"eyJhbGci.x.y\\\", \\\"ExpiresIn\\\": 60 }\""));
        Assert.Equal(("", 0), ResponseParser.ParseAccessToken("not json"));
    }

    [Theory]
    [InlineData("""{ "Response": "BSC4002" }""", "BSC4002", "Username and/or password is incorrect.")]
    [InlineData("""{ "Error": "BSC4008" }""", "BSC4008", "Username not found.")]
    [InlineData("Required parameter(s) missing!", "", "Required parameter(s) missing!")]
    [InlineData("", "", "")]
    public void RestError_IsRead(string body, string code, string message)
    {
        var (c, m) = ResponseParser.ParseRestError(body);
        Assert.Equal(code, c);
        Assert.Equal(message, m);
    }

    // ── Status code helpers ─────────────────────────────────────────────────

    [Theory]
    [InlineData("BSC4022", true)]
    [InlineData("bsc4022", true)]
    [InlineData("BSC40222", false)]
    [InlineData("Allow", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStatusCode(string? value, bool expected) =>
        Assert.Equal(expected, BentenStatusCodes.IsStatusCode(value));
}
