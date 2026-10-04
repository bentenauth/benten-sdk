// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Requests approval for a credit card transaction.
/// Benten sends a push notification to the card holder's registered device;
/// the user taps Allow or Deny and the result is returned on the WebSocket.
/// <br/>
/// WebSocket endpoint: <c>clients/creditcard/transaction</c> (relative to the <see cref="BentenClient"/> base URL)
/// </summary>
public class CCTransactionRequest : ClientRequest
{
    /// <summary>
    /// Initialises a new <see cref="CCTransactionRequest"/> with
    /// <see cref="ClientRequest.RequestType"/> set to <c>"CCTransaction"</c>.
    /// </summary>
    public CCTransactionRequest() : base("CCTransaction") { }

    /// <summary>
    /// The Benten-assigned ID of the credit card registered by the user.
    /// Obtain this ID when the card is registered via the Benten API.
    /// </summary>
    public string CreditCardId { get; set; } = string.Empty;

    /// <summary>
    /// The transaction amount as a display string (e.g. "$149.99").
    /// </summary>
    public string Amount { get; set; } = string.Empty;

    /// <summary>
    /// The retailer or merchant name (e.g. "Home Depot", "www.amazon.com").
    /// Displayed on the user's notification.
    /// </summary>
    public string Retailer { get; set; } = string.Empty;
}
