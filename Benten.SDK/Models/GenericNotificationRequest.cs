// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Requests approval for any custom action via a generic push notification.
/// The notification displays up to three labeled fields that you define.
/// Useful for approval workflows that don't fit the Login, CC, or IoT categories.
/// <br/>
/// WebSocket endpoint: <c>clients/generic/notification/</c> (relative to the <see cref="BentenClient"/> base URL)
/// </summary>
public class GenericNotificationRequest : ClientRequest
{
    /// <summary>
    /// Initialises a new <see cref="GenericNotificationRequest"/> with
    /// <see cref="ClientRequest.RequestType"/> set to <c>"GenericNotification"</c>.
    /// </summary>
    public GenericNotificationRequest() : base("GenericNotification") { }

    /// <summary>
    /// A short sub-type label shown on the notification
    /// (e.g. "Medical Data", "Document Approval", "Login").
    /// </summary>
    public string NotificationSubType { get; set; } = string.Empty;

    /// <summary>
    /// The country associated with the Benten user's phone number (e.g. "United States").
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// The Benten user's phone number (digits only, no country code).
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Label for the first data field (e.g. "Doctor").</summary>
    public string Line1Placeholder { get; set; } = string.Empty;

    /// <summary>Value for the first data field (e.g. "Dr. Jane Smith").</summary>
    public string Line1 { get; set; } = string.Empty;

    /// <summary>Label for the second data field (e.g. "Patient").</summary>
    public string Line2Placeholder { get; set; } = string.Empty;

    /// <summary>Value for the second data field (e.g. "John Doe").</summary>
    public string Line2 { get; set; } = string.Empty;

    /// <summary>Label for the third data field (e.g. "Reason").</summary>
    public string Line3Placeholder { get; set; } = string.Empty;

    /// <summary>Value for the third data field (e.g. "Review test results").</summary>
    public string Line3 { get; set; } = string.Empty;
}
