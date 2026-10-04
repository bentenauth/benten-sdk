// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

using System.Net.WebSockets;
using System.Text;
using Benten.SDK.Internal;
using Benten.SDK.Models;
using Newtonsoft.Json;

namespace Benten.SDK;

/// <summary>
/// Main entry point for the Benten Authentication and Authorization SDK.
///
/// <para>
/// The Benten platform lets your application request user approval for sensitive
/// actions — login, credit card transactions, IoT setting changes, or any custom
/// event — by sending a push notification to the user's registered mobile device.
/// The user approves or denies the request in the Benten App, and the result is
/// delivered back to your application over a WebSocket connection.
/// </para>
///
/// <example>
/// <code>
/// var benten = new BentenClient("wss://api.bententechnologies.com/v2/");
///
/// // 1. Obtain a Bearer token using your Benten developer credentials
/// string jwt = await benten.GetBearerTokenAsync("myusername", "mypassword");
///
/// // 2. Request login approval for a user
/// var result = await benten.RequestLoginApprovalAsync(jwt, new LoginRequest
/// {
///     Country     = "United States",
///     PhoneNumber = "6501234567",
///     Username    = "alice@example.com"
/// });
///
/// switch (result.Outcome)
/// {
///     case ApprovalOutcome.Approved: Console.WriteLine("Login approved!"); break;
///     case ApprovalOutcome.Denied:   Console.WriteLine("Login denied by user."); break;
///     case ApprovalOutcome.TimedOut: Console.WriteLine("User did not respond."); break;
///     case ApprovalOutcome.Error:    Console.WriteLine($"Error: {result.Error}"); break;
/// }
/// </code>
/// </example>
/// </summary>
public class BentenClient
{
    /// <summary>
    /// The production Benten API endpoint, including the API version segment.
    /// </summary>
    public const string DefaultServerUrl = "wss://api.bententechnologies.com/v2/";

    /// <summary>
    /// The API version segment the SDK targets. Appended to the base URL when
    /// the caller does not supply a version segment.
    /// </summary>
    public const string ApiVersion = "v2";

    private readonly string _baseWssUrl;   // e.g. "wss://api.bententechnologies.com/v2/"
    private readonly string _baseHttpsUrl; // e.g. "https://api.bententechnologies.com/v2/"
    private readonly HttpClient _httpClient;

    // The Benten API waits up to 60 seconds for the user to tap Allow or Deny, then replies
    // with NoResponse. The SDK waits 15 seconds longer so that reply arrives over a slow
    // network instead of being cut off by the SDK's own timeout.
    private static readonly TimeSpan DefaultReceiveTimeout = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    // Transport limits. Internal so tests can shorten them; the defaults are what ships.

    /// <summary>How long to wait for the Benten API's reply after sending a request.</summary>
    internal TimeSpan ResponseTimeout { get; set; } = DefaultReceiveTimeout;

    /// <summary>How long to wait for the WebSocket connection to open.</summary>
    internal TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>How long to spend sending the close message once the reply has arrived.</summary>
    internal TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The largest reply the SDK accepts. Real replies are well under 10 KB.</summary>
    internal int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Initialises a new <see cref="BentenClient"/> pointing at the production
    /// Benten API (<see cref="DefaultServerUrl"/>).
    /// </summary>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use for REST calls. If null, a new
    /// instance is created.
    /// </param>
    public BentenClient(HttpClient? httpClient = null)
        : this(DefaultServerUrl, httpClient)
    {
    }

    /// <summary>
    /// Initialises a new <see cref="BentenClient"/>.
    /// </summary>
    /// <param name="serverBaseUrl">
    /// Base URL of the Benten API server, e.g.
    /// <c>wss://api.bententechnologies.com/v2/</c> or
    /// <c>https://api.bententechnologies.com/v2/</c>.
    /// The SDK converts between wss:// and https:// as needed. If the URL does
    /// not end with an API version segment (such as <c>/v2</c>), <c>/v2</c> is
    /// appended automatically, so <c>wss://api.bententechnologies.com/</c> also works.
    /// </param>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use for REST calls. If null, a new
    /// instance is created. Supply your own to share connection pools or add
    /// custom handlers (e.g. logging, retry).
    /// </param>
    public BentenClient(string serverBaseUrl, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            throw new ArgumentException("serverBaseUrl must not be empty.", nameof(serverBaseUrl));

        // Normalise the base URL — ensure it ends with "/" and targets an API
        // version. All Benten endpoints live under /v2, so a bare host such as
        // "wss://api.bententechnologies.com/" becomes ".../v2/".
        serverBaseUrl = serverBaseUrl.Trim().TrimEnd('/');
        if (!EndsWithVersionSegment(serverBaseUrl))
            serverBaseUrl += "/" + ApiVersion;
        serverBaseUrl += "/";

        // Derive wss and https variants
        if (serverBaseUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            _baseWssUrl   = serverBaseUrl;
            _baseHttpsUrl = "https://" + serverBaseUrl["wss://".Length..];
        }
        else if (serverBaseUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
        {
            _baseWssUrl   = serverBaseUrl;
            _baseHttpsUrl = "http://" + serverBaseUrl["ws://".Length..];
        }
        else if (serverBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _baseHttpsUrl = serverBaseUrl;
            _baseWssUrl   = "wss://" + serverBaseUrl["https://".Length..];
        }
        else
        {
            _baseHttpsUrl = serverBaseUrl;
            _baseWssUrl   = "ws://" + serverBaseUrl["http://".Length..];
        }

        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Returns true if the last path segment of <paramref name="url"/> is an
    /// API version such as "v2" or "v3".
    /// </summary>
    private static bool EndsWithVersionSegment(string url)
    {
        int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        int pathStart = url.IndexOf('/', schemeEnd < 0 ? 0 : schemeEnd + 3);
        if (pathStart < 0) return false; // host only, no path

        string lastSegment = url[(url.LastIndexOf('/') + 1)..];
        return lastSegment.Length > 1
            && (lastSegment[0] == 'v' || lastSegment[0] == 'V')
            && lastSegment[1..].All(char.IsDigit);
    }

    // -------------------------------------------------------------------------
    // Authentication
    // -------------------------------------------------------------------------

    /// <summary>
    /// Obtains a Bearer JWT token from the Benten API using your developer
    /// credentials. This token must be supplied to all WebSocket request methods.
    /// </summary>
    /// <param name="username">Your Benten developer username.</param>
    /// <param name="password">Your Benten developer password.</param>
    /// <param name="timeoutSeconds">
    /// Desired token lifetime in seconds. Defaults to 4 hours (14400 s).
    /// Pass 0 to use the server default.
    /// </param>
    /// <returns>A JWT string to use as a Bearer token.</returns>
    /// <exception cref="BentenException">
    /// Thrown if the server returns an error or the HTTP request fails.
    /// <see cref="BentenException.ErrorCode"/> holds the Benten status code
    /// (e.g. "BSC4002" for a wrong password) when the server supplies one.
    /// </exception>
    public async Task<string> GetBearerTokenAsync(
        string username,
        string password,
        int timeoutSeconds = 0)
    {
        var payload = new
        {
            Username = username,
            Password = password,
            Timeout  = timeoutSeconds
        };

        var content  = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_baseHttpsUrl + "auth/token", content);

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var (code, message) = ResponseParser.ParseRestError(json);
            string detail = BentenResult.FormatError(code, message);
            throw new BentenException(
                $"GetBearerToken failed (HTTP {(int)response.StatusCode}): {(detail.Length > 0 ? detail : json)}",
                code,
                response.StatusCode);
        }

        // Response shape: { "Token": "eyJ..." }
        var token = ResponseParser.ParseBearerToken(json);
        if (string.IsNullOrWhiteSpace(token))
            throw new BentenException($"Token not found in response: {json}");

        return token;
    }

    // -------------------------------------------------------------------------
    // Login approval
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests user approval for a login attempt.
    /// A push notification is sent to the user's registered Benten App;
    /// the user taps Allow or Deny and the result is returned.
    /// </summary>
    /// <param name="jwt">Bearer token from <see cref="GetBearerTokenAsync"/>.</param>
    /// <param name="request">Login request details.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<BentenResult> RequestLoginApprovalAsync(
        string jwt,
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        request.JWT = jwt;
        var raw = await SendWebSocketRequestAsync("clients/loginrequest/phonenumber", request, cancellationToken);
        return ParseBentenResult(raw);
    }

    // -------------------------------------------------------------------------
    // Credit card transaction approval
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests user approval for a credit card transaction.
    /// A push notification is sent to the card holder's registered Benten App.
    /// </summary>
    /// <param name="jwt">Bearer token from <see cref="GetBearerTokenAsync"/>.</param>
    /// <param name="request">Transaction request details.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<BentenResult> RequestCCTransactionApprovalAsync(
        string jwt,
        CCTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        request.JWT = jwt;
        var raw = await SendWebSocketRequestAsync("clients/creditcard/transaction", request, cancellationToken);
        return ParseBentenResult(raw);
    }

    // -------------------------------------------------------------------------
    // IoT setting change approval
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests user approval for an IoT device setting change.
    /// A push notification is sent to the device owner's registered Benten App.
    /// </summary>
    /// <param name="jwt">Bearer token from <see cref="GetBearerTokenAsync"/>.</param>
    /// <param name="request">IoT setting change request details.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<BentenResult> RequestIoTSettingApprovalAsync(
        string jwt,
        IoTSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        request.JWT = jwt;
        var raw = await SendWebSocketRequestAsync("clients/iotsetting/update", request, cancellationToken);
        return ParseBentenResult(raw);
    }

    // -------------------------------------------------------------------------
    // Generic notification approval
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests user approval for any custom action via a generic notification.
    /// Useful for workflows that don't fit Login, CC, or IoT categories.
    /// </summary>
    /// <param name="jwt">Bearer token from <see cref="GetBearerTokenAsync"/>.</param>
    /// <param name="request">Generic notification request details.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<BentenResult> RequestGenericNotificationApprovalAsync(
        string jwt,
        GenericNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        request.JWT = jwt;
        var raw = await SendWebSocketRequestAsync("clients/generic/notification/", request, cancellationToken);
        return ParseBentenResult(raw);
    }

    // -------------------------------------------------------------------------
    // Auth token request (push-to-approve token issuance)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests a Benten-issued JWT for a user, confirmed by the user tapping
    /// Allow on their Benten App. Unlike <see cref="GetBearerTokenAsync"/>, this
    /// flow does not require the user's password — the token is issued only after
    /// explicit push-notification consent.
    /// </summary>
    /// <param name="request">Auth token request details (country, phone number).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="AuthTokenResult"/> containing the issued JWT on success.</returns>
    public async Task<AuthTokenResult> RequestAuthTokenAsync(
        AuthTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var raw = await SendWebSocketRequestAsync("auth/authtokenrequest/", request, cancellationToken);
        return ParseAuthTokenResult(raw);
    }

    // -------------------------------------------------------------------------
    // WebSocket transport (internal)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens a WebSocket to <paramref name="route"/>, sends <paramref name="requestObj"/>
    /// as JSON, reads the server's complete reply, closes the connection, and
    /// returns the reply as a string.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    /// <exception cref="BentenException">
    /// The connection could not be opened, failed, was closed by the server before
    /// it replied, the reply was too large, or no reply arrived in time.
    /// </exception>
    private async Task<string> SendWebSocketRequestAsync(
        string route,
        object requestObj,
        CancellationToken cancellationToken)
    {
        string url = _baseWssUrl + route;
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = KeepAliveInterval;

        // 1. Connect, with its own time limit.
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connectCts.CancelAfter(ConnectTimeout);
            try
            {
                await ws.ConnectAsync(new Uri(url), connectCts.Token).ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception ex) when (connectCts.IsCancellationRequested)
            {
                throw new BentenException(
                    $"Could not connect to {url} within {ConnectTimeout.TotalSeconds:0} seconds.", ex);
            }
            catch (Exception ex)
            {
                throw new BentenException($"Failed to connect to {url}: {ex.Message}", ex);
            }
        }

        // 2. Send the request and read the complete reply, within the response time limit.
        string rawResponse;
        using (var responseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            responseCts.CancelAfter(ResponseTimeout);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestObj));
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, responseCts.Token)
                        .ConfigureAwait(false);

                rawResponse = await ReceiveMessageAsync(ws, route, responseCts.Token).ConfigureAwait(false);
            }
            catch (BentenException)
            {
                throw;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception ex) when (responseCts.IsCancellationRequested)
            {
                throw new BentenException(
                    $"No reply from the Benten API ({route}) within {ResponseTimeout.TotalSeconds:0} seconds.", ex);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException)
            {
                throw new BentenException($"The connection to the Benten API failed ({route}): {ex.Message}", ex);
            }
        }

        // 3. Close politely without waiting for the server; the reply is already in hand.
        await CloseQuietlyAsync(ws).ConfigureAwait(false);

        return rawResponse;
    }

    /// <summary>
    /// Reads one complete WebSocket message, which may arrive in several frames,
    /// and decodes it as UTF-8 once all of it has arrived.
    /// </summary>
    private async Task<string> ReceiveMessageAsync(ClientWebSocket ws, string route, CancellationToken token)
    {
        var buffer = new byte[4 * 1024];
        using var message = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                string reason = result.CloseStatusDescription ?? string.Empty;
                string status = result.CloseStatus?.ToString() ?? "no close status";
                throw new BentenException(
                    $"The Benten API closed the connection ({route}) without replying: {status}" +
                    (reason.Length > 0 ? $", \"{reason}\"." : "."),
                    BentenStatusCodes.FindCode(reason),
                    null);
            }

            if (message.Length + result.Count > MaxResponseBytes)
            {
                throw new BentenException(
                    $"The reply from the Benten API ({route}) is larger than {MaxResponseBytes / 1024} KB.");
            }

            message.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    /// <summary>
    /// Sends the WebSocket close message without waiting for the server to answer it,
    /// and ignores any failure: by now the reply has been received.
    /// </summary>
    private async Task CloseQuietlyAsync(ClientWebSocket ws)
    {
        if (ws.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;

        using var cts = new CancellationTokenSource(CloseTimeout);
        try
        {
            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Ignore: the request already succeeded.
        }
    }

    // -------------------------------------------------------------------------
    // Response parsers (internal) — see Internal/ResponseParser.cs
    // -------------------------------------------------------------------------

    private static BentenResult ParseBentenResult(string raw) => ResponseParser.ParseApproval(raw);

    private static AuthTokenResult ParseAuthTokenResult(string raw) => ResponseParser.ParseAuthToken(raw);
}
