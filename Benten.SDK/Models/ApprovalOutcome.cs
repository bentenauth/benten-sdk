// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// The outcome of a Benten approval request.
/// </summary>
/// <remarks>
/// <see cref="Error"/> is the default value, so an uninitialised or unparseable
/// result is never mistaken for an approval.
/// </remarks>
public enum ApprovalOutcome
{
    /// <summary>
    /// The request failed. See <c>ErrorCode</c> and <c>ErrorMessage</c> on the result.
    /// </summary>
    Error = 0,

    /// <summary>The user tapped Allow.</summary>
    Approved = 1,

    /// <summary>The user tapped Deny.</summary>
    Denied = 2,

    /// <summary>The user did not respond within 60 seconds (<c>NoResponse</c>).</summary>
    TimedOut = 3
}
