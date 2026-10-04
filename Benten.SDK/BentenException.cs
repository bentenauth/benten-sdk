// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

using System.Net;

namespace Benten.SDK;

/// <summary>
/// Exception thrown when a Benten SDK operation fails at the transport or
/// protocol level (connection refused, WebSocket error, timeout, etc.).
/// For errors returned by an approval request (e.g. user not found, device
/// inactive), check <see cref="Models.BentenResult.Outcome"/> and
/// <see cref="Models.BentenResult.ErrorCode"/> instead: those are returned in
/// the result, not thrown.
/// </summary>
public class BentenException : Exception
{
    /// <summary>Initialises a new <see cref="BentenException"/> with the specified error message.</summary>
    /// <param name="message">A human-readable description of the error.</param>
    public BentenException(string message) : base(message) { }

    /// <summary>
    /// Initialises a new <see cref="BentenException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="inner">
    /// The exception that is the cause of the current exception.
    /// Pass <see langword="null"/> if no inner exception is available.
    /// </param>
    public BentenException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initialises a new <see cref="BentenException"/> with a Benten status code
    /// and the HTTP status returned by the server.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="errorCode">The Benten status code (e.g. "BSC4002"), or an empty string.</param>
    /// <param name="statusCode">The HTTP status code, if the error came from an HTTP response.</param>
    public BentenException(string message, string errorCode, HttpStatusCode? statusCode) : base(message)
    {
        ErrorCode  = errorCode ?? string.Empty;
        StatusCode = statusCode;
    }

    /// <summary>
    /// The Benten status code (e.g. "BSC4002") returned by the server, or an
    /// empty string if there was none. See <see cref="BentenStatusCodes.Describe"/>.
    /// </summary>
    public string ErrorCode { get; } = string.Empty;

    /// <summary>The HTTP status code, if the error came from an HTTP response.</summary>
    public HttpStatusCode? StatusCode { get; }
}
