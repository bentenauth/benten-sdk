// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Represents the outcome of an <see cref="AuthTokenRequest"/>.
/// On approval, contains a JWT the caller can use as a Bearer token.
/// </summary>
public class AuthTokenResult
{
    /// <summary>
    /// The request ID echoed by the server (from <c>RequestId</c> or <c>_id</c> in the reply).
    /// Matches <see cref="AuthTokenRequest.RequestId"/>.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// The JWT Bearer token issued by Benten after the user approves the request.
    /// Empty unless <see cref="Outcome"/> is <see cref="ApprovalOutcome.Approved"/>.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Country echoed from the request.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Phone number echoed from the request.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// The outcome of the request. Defaults to <see cref="ApprovalOutcome.Error"/>.
    /// </summary>
    public ApprovalOutcome Outcome { get; set; } = ApprovalOutcome.Error;

    /// <summary>The raw <c>Response</c> value from the server, if any.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>The Benten status code (e.g. "BSC4067") when the request failed.</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>A human-readable description of the error. Empty when there is no error.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The error as a single display string, e.g. "BSC4067: Phone number not found."</summary>
    public string Error => BentenResult.FormatError(ErrorCode, ErrorMessage);

    /// <summary>Returns true if the user approved and a token was issued.</summary>
    public bool IsSuccess => Outcome == ApprovalOutcome.Approved && !string.IsNullOrWhiteSpace(Token);

    /// <summary>Returns true if the user denied the request.</summary>
    public bool IsDenied => Outcome == ApprovalOutcome.Denied;

    /// <summary>Returns true if the user did not respond within 60 seconds (<c>NoResponse</c>).</summary>
    public bool IsTimedOut => Outcome == ApprovalOutcome.TimedOut;

    /// <summary>Returns true if the request failed. See <see cref="ErrorCode"/> and <see cref="ErrorMessage"/>.</summary>
    public bool HasError => Outcome == ApprovalOutcome.Error;

    /// <summary>The full raw string received from the server.</summary>
    public string RawResponse { get; set; } = string.Empty;
}
