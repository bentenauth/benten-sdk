// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

using System.Text.Json;
using Benten.SDK.Models;

namespace Benten.SDK.Internal;

/// <summary>
/// Parses responses from the Benten API into SDK result types.
/// </summary>
/// <remarks>
/// The approval endpoints reply with JSON such as
/// <c>{ "_id": "...", "Response": "Allow" }</c>. The request ID arrives as
/// <c>RequestId</c> or <c>_id</c>, depending on the reply. Errors arrive in the same
/// <c>Response</c> field as a Benten status code (e.g. <c>"BSC4019"</c>), in an
/// <c>Error</c> field, or occasionally as plain text. This parser handles all
/// three, plus JSON that the server has serialized twice.
/// </remarks>
internal static class ResponseParser
{
    private const string Allow      = "Allow";
    private const string Deny       = "Deny";
    private const string NoResponse = "NoResponse";

    /// <summary>
    /// Parses the reply from an approval endpoint (login, credit card, IoT, generic).
    /// </summary>
    public static BentenResult ParseApproval(string? raw)
    {
        var result = new BentenResult { RawResponse = raw ?? string.Empty };

        string response, error;
        if (TryParseObject(raw, out var obj))
        {
            result.RequestId = ReadRequestId(obj);
            response = GetString(obj, "Response");
            error    = GetString(obj, "Error");
        }
        else
        {
            // Not JSON: the server sent plain text (or nothing).
            response = raw?.Trim() ?? string.Empty;
            error    = string.Empty;
        }

        var (outcome, code, message) = Classify(response, error);
        result.Response     = response;
        result.Outcome      = outcome;
        result.ErrorCode    = code;
        result.ErrorMessage = message;
        return result;
    }

    /// <summary>
    /// Reads an error code and message from a failed REST response body
    /// (e.g. <c>auth/token</c>). Returns empty strings if none can be found.
    /// </summary>
    public static (string Code, string Message) ParseRestError(string? body)
    {
        string response, error;
        if (TryParseObject(body, out var obj))
        {
            response = GetString(obj, "Response");
            error    = FirstNonEmpty(GetString(obj, "Error"), GetString(obj, "Message"),
                                     GetString(obj, "BentenStatusCode"));
        }
        else
        {
            response = body?.Trim() ?? string.Empty;
            error    = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(response) && string.IsNullOrWhiteSpace(error))
            return (string.Empty, string.Empty);

        var (_, code, message) = Classify(response, error);
        return (code, message);
    }

    /// <summary>
    /// Reads the access token and its lifetime from a successful <c>auth/token</c>
    /// response body: <c>{ "Token": "eyJ...", "ExpiresIn": 3600 }</c>.
    /// ExpiresIn is 0 if missing.
    /// </summary>
    public static (string Token, int ExpiresInSeconds) ParseAccessToken(string? body)
    {
        if (!TryParseObject(body, out var obj))
            return (string.Empty, 0);

        int.TryParse(GetString(obj, "ExpiresIn"), System.Globalization.NumberStyles.Integer,
                     System.Globalization.CultureInfo.InvariantCulture, out int expiresIn);
        return (GetString(obj, "Token"), Math.Max(0, expiresIn));
    }

    /// <summary>
    /// Decides the outcome from the <c>Response</c> and <c>Error</c> fields.
    /// </summary>
    internal static (ApprovalOutcome Outcome, string Code, string Message) Classify(string response, string error)
    {
        response = response?.Trim() ?? string.Empty;
        error    = error?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(error))
        {
            if (Is(response, Allow))      return (ApprovalOutcome.Approved, string.Empty, string.Empty);
            if (Is(response, Deny))       return (ApprovalOutcome.Denied,   string.Empty, string.Empty);
            if (Is(response, NoResponse)) return (ApprovalOutcome.TimedOut, string.Empty, string.Empty);
        }

        // Anything else is an error. Prefer the Error field, then Response.
        string detail = error.Length > 0 ? error : response;
        if (detail.Length == 0)
            return (ApprovalOutcome.Error, string.Empty, "The Benten API returned an empty response.");

        if (BentenStatusCodes.IsStatusCode(detail))
        {
            string code = detail.ToUpperInvariant();
            if (code == BentenStatusCodes.RequestDeniedByUser)
                return (ApprovalOutcome.Denied, string.Empty, string.Empty);

            return (ApprovalOutcome.Error, code, BentenStatusCodes.Describe(code));
        }

        // A message that contains a code, e.g. "BSC4019 - Invalid Token", or plain text.
        return (ApprovalOutcome.Error, BentenStatusCodes.FindCode(detail), detail);
    }

    /// <summary>
    /// Parses <paramref name="raw"/> as a JSON object. Unwraps JSON that was
    /// serialized more than once (a JSON string containing JSON).
    /// </summary>
    internal static bool TryParseObject(string? raw, out JsonElement obj)
    {
        obj = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string text = raw;
        for (int depth = 0; depth < 3; depth++)
        {
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(text);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return false;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                obj = root;
                return true;
            }

            if (root.ValueKind != JsonValueKind.String)
                return false;

            text = root.GetString() ?? string.Empty;
        }

        return false;
    }

    /// <summary>
    /// Returns a property's value as a string, matching the name case-insensitively.
    /// Missing or null properties return an empty string.
    /// </summary>
    internal static string GetString(JsonElement obj, string name)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads the echoed request ID. The Benten API returns it as <c>RequestId</c>
    /// in error and <c>NoResponse</c> replies, and as <c>_id</c> in Allow/Deny replies.
    /// </summary>
    internal static string ReadRequestId(JsonElement obj) =>
        FirstNonEmpty(GetString(obj, "RequestId"), GetString(obj, "_id"));

    private static bool Is(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
