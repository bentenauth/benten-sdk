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
    /// and an auto-generated <see cref="_id"/>.
    /// </summary>
    /// <param name="requestType">
    /// A string token identifying the request category (e.g. <c>"LoginRequest"</c>,
    /// <c>"CCTransaction"</c>). Each concrete subclass passes a fixed value here.
    /// </param>
    protected ClientRequest(string requestType)
    {
        RequestType = requestType;
        _id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Unique identifier for this request. Auto-generated if not provided.
    /// The server echoes this value back in its response so you can correlate
    /// responses to requests.
    /// </summary>
    public string _id { get; set; }

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
