using System.Net.WebSockets;
using System.Text;
using Benten.SDK;
using Benten.SDK.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Benten.SDK.Tests;

/// <summary>
/// Tests for how the SDK uses the API key: exchanging it for an access token,
/// reusing and renewing that token, and reporting a rejected key.
/// </summary>
public class ApiKeyTests
{
    private static LoginRequest Login() => new() { Country = "United States", PhoneNumber = "6501234567" };

    private static Task AllowAsync(WebSocket ws) =>
        ws.SendAsync(Encoding.UTF8.GetBytes("""{ "_id": "abc", "Response": "Allow" }"""),
                     WebSocketMessageType.Text, true, CancellationToken.None);

    [Fact]
    public void EmptyApiKey_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new BentenClient(""));
        Assert.Throws<ArgumentException>(() => new BentenClient("  ", "wss://example.com/v2/"));
    }

    [Fact]
    public async Task ApiKey_IsExchangedOnce_AndTheTokenIsReused()
    {
        var sentTokens = new System.Collections.Concurrent.ConcurrentBag<string>();
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            sentTokens.Add(System.Text.Json.JsonDocument.Parse(request).RootElement.GetProperty("JWT").GetString()!);
            await AllowAsync(ws);
        });

        var client = server.Client();
        Assert.True((await client.RequestLoginApprovalAsync(Login())).IsApproved);
        Assert.True((await client.RequestLoginApprovalAsync(Login())).IsApproved);

        Assert.Equal(TestServer.ApiKey, Assert.Single(server.TokenRequests));   // one exchange, with our key
        Assert.Equal("test-token-1,test-token-1", string.Join(",", sentTokens));
    }

    [Fact]
    public async Task ConcurrentRequests_ShareOneTokenExchange()
    {
        await using var server = await TestServer.StartAsync(async (ws, request) => await AllowAsync(ws));
        var client = server.Client();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.RequestLoginApprovalAsync(Login())));

        Assert.All(results, r => Assert.True(r.IsApproved));
        Assert.Single(server.TokenRequests);
    }

    [Fact]
    public async Task TokenCloseToExpiry_IsRenewed()
    {
        // Tokens that live 60 seconds are always inside the 5-minute renewal margin,
        // so each request gets a new one.
        await using var server = await TestServer.StartAsync(
            async (ws, request) => await AllowAsync(ws),
            (s, key) => Results.Text($$"""{ "Token": "short-{{s.NextTokenNumber()}}", "ExpiresIn": 60 }""", "application/json"));
        var client = server.Client();

        await client.RequestLoginApprovalAsync(Login());
        await client.RequestLoginApprovalAsync(Login());

        Assert.Equal(2, server.TokenRequests.Count);
    }

    [Fact]
    public async Task RejectedApiKey_ThrowsWithCode_AndNothingIsSent()
    {
        bool approvalRequested = false;
        await using var server = await TestServer.StartAsync(
            async (ws, request) => { approvalRequested = true; await AllowAsync(ws); },
            (s, key) => Results.Text("""{"Response":"BSC4076"}""", "application/json", statusCode: 401));

        var ex = await Assert.ThrowsAsync<BentenException>(() => server.Client().RequestLoginApprovalAsync(Login()));

        Assert.Equal("BSC4076", ex.ErrorCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("did not accept the API key", ex.Message);
        Assert.False(approvalRequested);
    }

    [Fact]
    public async Task TokenRejectedByServer_IsRenewedAndRequestRetriedOnce()
    {
        // The first token is refused (as if it had expired early); the second works.
        int connections = 0;
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            Interlocked.Increment(ref connections);
            string jwt = System.Text.Json.JsonDocument.Parse(request).RootElement.GetProperty("JWT").GetString()!;
            string reply = jwt == "test-token-1"
                ? """{ "RequestId": "abc", "Response": "BSC4019" }"""
                : """{ "_id": "abc", "Response": "Allow" }""";
            await ws.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, CancellationToken.None);
        });

        var result = await server.Client().RequestLoginApprovalAsync(Login());

        Assert.True(result.IsApproved);
        Assert.Equal(2, server.TokenRequests.Count);
        Assert.Equal(2, connections);
    }

    [Fact]
    public async Task TokenRejectedTwice_IsReturnedAsError_WithoutLooping()
    {
        int connections = 0;
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            Interlocked.Increment(ref connections);
            await ws.SendAsync(Encoding.UTF8.GetBytes("""{ "RequestId": "abc", "Response": "BSC4019" }"""),
                               WebSocketMessageType.Text, true, CancellationToken.None);
        });

        var result = await server.Client().RequestLoginApprovalAsync(Login());

        Assert.True(result.HasError);
        Assert.Equal("BSC4019", result.ErrorCode);
        Assert.Equal(2, connections);
    }

    [Fact]
    public async Task OtherErrors_AreNotRetried()
    {
        int connections = 0;
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            Interlocked.Increment(ref connections);
            await ws.SendAsync(Encoding.UTF8.GetBytes("""{ "RequestId": "abc", "Response": "BSC4022" }"""),
                               WebSocketMessageType.Text, true, CancellationToken.None);
        });

        var result = await server.Client().RequestLoginApprovalAsync(Login());

        Assert.Equal("BSC4022", result.ErrorCode);
        Assert.Equal(1, connections);
        Assert.Single(server.TokenRequests);
    }

    [Fact]
    public void AccessToken_IsNotPartOfThePublicRequestModel()
    {
        Assert.Null(typeof(LoginRequest).GetProperty("JWT"));   // internal: set by BentenClient only
    }
}
