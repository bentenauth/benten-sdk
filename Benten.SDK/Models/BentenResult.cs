// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Represents the outcome of a Benten WebSocket approval request
/// (Login, CC Transaction, IoT Setting, Generic Notification).
/// </summary>
/// <example>
/// <code>
/// switch (result.Outcome)
/// {
///     case ApprovalOutcome.Approved: /* proceed */ break;
///     case ApprovalOutcome.Denied:   /* user said no */ break;
///     case ApprovalOutcome.TimedOut: /* user didn't answer */ break;
///     case ApprovalOutcome.Error:    Log(result.ErrorCode, result.ErrorMessage); break;
/// }
/// </code>
/// </example>
public class BentenResult
{
    /// <summary>
    /// The request ID echoed by the server (from <c>RequestId</c> or <c>_id</c> in the reply).
    /// Matches <see cref="ClientRequest.RequestId"/> of the request that produced this result.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// The outcome of the request. Defaults to <see cref="ApprovalOutcome.Error"/>,
    /// so only an explicit Allow from the user is ever treated as approved.
    /// </summary>
    public ApprovalOutcome Outcome { get; set; } = ApprovalOutcome.Error;

    /// <summary>
    /// The raw <c>Response</c> value from the server: "Allow", "Deny", "NoResponse",
    /// or a Benten status code such as "BSC4019" when the request failed.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// The Benten status code (e.g. "BSC4022") when <see cref="Outcome"/> is
    /// <see cref="ApprovalOutcome.Error"/>. Empty if the server sent no code.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable description of the error, e.g. "No registered device
    /// found for the given country and phone number." Empty when there is no error.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// The error as a single display string, e.g.
    /// "BSC4022: No registered device found for the given country and phone number."
    /// Empty when there is no error.
    /// </summary>
    public string Error => FormatError(ErrorCode, ErrorMessage);

    /// <summary>Returns true if the user explicitly approved the request.</summary>
    public bool IsApproved => Outcome == ApprovalOutcome.Approved;

    /// <summary>Returns true if the user explicitly denied the request.</summary>
    public bool IsDenied => Outcome == ApprovalOutcome.Denied;

    /// <summary>Returns true if the user did not respond within 60 seconds (<c>NoResponse</c>).</summary>
    public bool IsTimedOut => Outcome == ApprovalOutcome.TimedOut;

    /// <summary>Returns true if the request failed. See <see cref="ErrorCode"/> and <see cref="ErrorMessage"/>.</summary>
    public bool HasError => Outcome == ApprovalOutcome.Error;

    /// <summary>The full raw string received from the server.</summary>
    public string RawResponse { get; set; } = string.Empty;

    internal static string FormatError(string code, string message)
    {
        code    ??= string.Empty;
        message ??= string.Empty;

        if (code.Length == 0)    return message;
        if (message.Length == 0) return code;
        if (message.Contains(code, StringComparison.OrdinalIgnoreCase)) return message; // already includes the code
        return $"{code}: {message}";
    }
}
