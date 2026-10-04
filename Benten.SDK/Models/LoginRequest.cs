// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Requests approval for a user login attempt.
/// Benten sends a push notification to the user's registered mobile device;
/// the user taps Allow or Deny and the result is returned on the WebSocket.
/// <br/>
/// WebSocket endpoint: <c>clients/loginrequest/phonenumber</c> (relative to the <see cref="BentenClient"/> base URL)
/// </summary>
public class LoginRequest : ClientRequest
{
    /// <summary>
    /// Initialises a new <see cref="LoginRequest"/> with
    /// <see cref="ClientRequest.RequestType"/> set to <c>"LoginRequest"</c>.
    /// </summary>
    public LoginRequest() : base("LoginRequest") { }

    /// <summary>
    /// The email address or username attempting to log in.
    /// Displayed on the user's Benten App notification.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The country associated with the Benten user's phone number (e.g. "United States").
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// The Benten user's phone number (digits only, no country code).
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;
}
