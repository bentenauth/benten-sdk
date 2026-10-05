// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Base class for all Benten WebSocket request messages.
/// </summary>
public abstract class ClientRequest
{
    /// <summary>
    /// Initialises a new <see cref="ClientRequest"/> with the specified request type
    /// and a new, unique <see cref="RequestId"/>.
    /// </summary>
    /// <param name="requestType">
    /// A string token identifying the request category (e.g. <c>"LoginRequest"</c>,
    /// <c>"CCTransaction"</c>). Each concrete subclass passes a fixed value here.
    /// </param>
    protected ClientRequest(string requestType)
    {
        RequestType = requestType;
        RequestId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Unique identifier for this request, sent to the server as <c>RequestId</c>.
    /// A new GUID is generated for every request. If you set your own value, it
    /// must be unique per request: never reuse an ID or use a fixed value.
    /// The server echoes it back, and it appears as <see cref="BentenResult.RequestId"/>.
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// Identifies the type of request. Set automatically by each subclass.
    /// </summary>
    public string RequestType { get; }

    /// <summary>
    /// The Bearer JWT token obtained from <see cref="BentenClient.GetBearerTokenAsync"/>.
    /// Required for all client WebSocket endpoints.
    /// </summary>
    public string JWT { get; set; } = string.Empty;
}
