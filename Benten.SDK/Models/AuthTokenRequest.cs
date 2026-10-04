// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Requests a Benten-issued JWT token for a user, with the user's consent
/// confirmed via a push notification on their registered device.
/// The resulting token can be used as a Bearer token for subsequent API calls.
/// <br/>
/// WebSocket endpoint: <c>auth/authtokenrequest/</c> (relative to the <see cref="BentenClient"/> base URL)
/// <br/>
/// Note: This endpoint does NOT require a JWT in the request — it IS the token
/// acquisition flow.
/// </summary>
public class AuthTokenRequest
{
    /// <summary>
    /// Unique identifier for this request. Auto-generated if not set.
    /// Echoed back in the server response.
    /// </summary>
    public string _id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The country associated with the Benten user's phone number (e.g. "United States").
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// The Benten user's phone number (digits only, no country code).
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Desired token lifetime in seconds. Pass 0 to use the server default (4 hours).
    /// </summary>
    public int Timeout { get; set; } = 0;
}
