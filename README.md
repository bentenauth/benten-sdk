# Benten SDK

A .NET 8 client library for the [Benten](https://bententechnologies.com) Authentication and Authorization Platform.

Benten lets your application request explicit user approval for sensitive actions — login attempts, credit card transactions, IoT device changes, or any custom event — by sending a push notification to the user's registered smartphone. The user taps **Allow** or **Deny** in the Benten App, and the result is delivered back to your application over a WebSocket.

---

## How it works

```
Your App  ──WebSocket──►  Benten API  ──FCM push──►  User's Phone (Benten App)
          ◄──────────────              ◄─────────────  (Allow / Deny tap)
```

1. Your app connects to Benten API over a WebSocket and sends an approval request.
2. Benten API sends a Firebase Cloud Messaging (FCM) push notification to the user's phone.
3. The user reviews the request in the Benten App and taps Allow or Deny.
4. Benten API delivers the response back to your WebSocket connection.
5. Your app reads the result and proceeds accordingly.

---

## Prerequisites

- .NET 8 or later
- A Benten developer account and an **API key**: sign up and create a key on the [developer dashboard](https://bententechnologies.com/developers.html)
- The user must have the Benten App installed and a registered device

---

## Installation

```bash
dotnet add package Benten.SDK
```

Or add to your `.csproj`:

```xml
<PackageReference Include="Benten.SDK" Version="1.0.0" />
```

---

## Quick start

```csharp
using Benten.SDK;
using Benten.SDK.Models;

// Load the API key from configuration or a secret store; don't hard-code it.
var benten = new BentenClient(Environment.GetEnvironmentVariable("BENTEN_API_KEY")!);

// Request login approval for a user
var result = await benten.RequestLoginApprovalAsync(new LoginRequest
{
    Country     = "United States",
    PhoneNumber = "6501234567",       // User's phone number on file in Benten
    Username    = "alice@example.com" // Display on the notification
});

switch (result.Outcome)
{
    case ApprovalOutcome.Approved:
        Console.WriteLine("Login approved — grant access.");
        break;
    case ApprovalOutcome.Denied:
        Console.WriteLine("Login denied by user.");
        break;
    case ApprovalOutcome.TimedOut:
        Console.WriteLine("User did not respond within 60 seconds.");
        break;
    case ApprovalOutcome.Error:
        // e.g. "BSC4022: No registered device found for the given country and phone number."
        Console.WriteLine($"Error: {result.Error}");
        break;
}
```

---

## API keys

Create API keys on the [developer dashboard](https://bententechnologies.com/developers.html): sign in, choose **Create API key**, and copy the key (it starts with `bnt_sk_`). The full key is shown only once.

- **Keep it on your server.** Store it in configuration, an environment variable or a secret store. Never put it in a mobile app, a web page or a public repository. Mobile and web apps should call your own backend, and your backend calls Benten.
- **One key per app or environment** (e.g. "Production web server", "Staging"), so you can revoke one without affecting the others.
- **Rotating a key:** create the new key, deploy it, then revoke the old one on the dashboard. A revoked key stops working within an hour.

The SDK exchanges the key for a short-lived access token (one hour) and renews it before it expires, so you never handle tokens yourself. Create one `BentenClient` and reuse it; it is safe to use from several threads at once.

---

## API reference

### `BentenClient(apiKey)` / `BentenClient(apiKey, serverBaseUrl)`

Creates a client for the Benten API.

| Parameter | Description |
|-----------|-------------|
| `apiKey` | Your API key from the developer dashboard (`bnt_sk_…`). |
| `serverBaseUrl` | Optional. Base URL, e.g. `wss://api.bententechnologies.com/v2/` (the default). The SDK handles wss↔https conversion automatically, and appends `/v2` if the URL has no version segment. |
| `httpClient` | Optional. The `HttpClient` used to get access tokens. |

---

### `RequestLoginApprovalAsync(LoginRequest, cancellationToken?)`

Requests user approval for a login attempt.

```csharp
var result = await benten.RequestLoginApprovalAsync(new LoginRequest
{
    Country     = "United States",
    PhoneNumber = "6501234567",
    Username    = "alice@example.com"
});
```

---

### `RequestCCTransactionApprovalAsync(CCTransactionRequest, cancellationToken?)`

Requests user approval for a credit card transaction.

```csharp
var result = await benten.RequestCCTransactionApprovalAsync(new CCTransactionRequest
{
    CreditCardId = "card-id-from-benten",  // Issued when the card is registered
    Amount       = "$149.99",
    Retailer     = "Home Depot"
});
```

---

### `RequestIoTSettingApprovalAsync(IoTSettingRequest, cancellationToken?)`

Requests user approval for an IoT device configuration change.

```csharp
var result = await benten.RequestIoTSettingApprovalAsync(new IoTSettingRequest
{
    Name          = "Living Room Thermostat",
    MacAddress    = "AA:BB:CC:DD:EE:01",
    SettingChange = "Change temperature from 72°F to 68°F"
});
```

---

### `RequestGenericNotificationApprovalAsync(GenericNotificationRequest, cancellationToken?)`

Requests user approval for any custom action via a three-field notification.

```csharp
var result = await benten.RequestGenericNotificationApprovalAsync(new GenericNotificationRequest
{
    NotificationSubType = "Document Approval",
    Country             = "United States",
    PhoneNumber         = "6501234567",
    Line1Placeholder    = "Document",
    Line1               = "Q3 Financial Report",
    Line2Placeholder    = "Requested By",
    Line2               = "Finance Team",
    Line3Placeholder    = "Action",
    Line3               = "Approve for publication"
});
```

---

### `BentenResult` properties

| Property | Description |
|----------|-------------|
| `Outcome` | `ApprovalOutcome.Approved`, `Denied`, `TimedOut` or `Error`. Defaults to `Error`, so only an explicit Allow counts as approved. |
| `IsApproved` | `true` if the user tapped Allow |
| `IsDenied` | `true` if the user tapped Deny |
| `IsTimedOut` | `true` if the user did not respond within 60 seconds (`NoResponse`) |
| `HasError` | `true` if the request failed |
| `ErrorCode` | Benten status code when the request failed, e.g. `BSC4022`. Empty if the server sent only a message. |
| `ErrorMessage` | Human-readable description, e.g. "No registered device found for the given country and phone number." |
| `Error` | `ErrorCode` and `ErrorMessage` combined for display, e.g. `BSC4022: No registered device found…` |
| `RequestId` | Your request's ID (`ClientRequest.RequestId`), echoed by the server so you can match replies to requests |
| `Response` | Raw `Response` value from the server ("Allow", "Deny", "NoResponse", or a status code) |
| `RawResponse` | Full string received from the server |

### Errors: returned vs. thrown

- **Returned:** errors reported by the Benten API for an approval request (invalid token, unknown phone number, inactive device…) come back in the result with `Outcome == ApprovalOutcome.Error`. Nothing is thrown.
- **Thrown as `BentenException`:** a rejected API key, and problems talking to the Benten API:
  - the API key is invalid or has been revoked: `ErrorCode` is `BSC4076` and `StatusCode` is 401;
  - the connection can't be opened within 15 seconds, or fails partway through;
  - the server closes the connection without replying (the message includes its reason, and `ErrorCode` holds any status code in it);
  - no reply arrives within 75 seconds (the server itself replies `NoResponse` after 60 seconds, so this means the server or network is in trouble);
  - for any other failed token request, `ErrorCode` holds the status code (e.g. `BSC4032` for an inactive account) and `StatusCode` holds the HTTP status.
- **Thrown as `OperationCanceledException`:** you cancelled the request through the `CancellationToken` you passed in. This is never reported as a timeout, so you can tell the two apart.

```csharp
try
{
    var result = await benten.RequestLoginApprovalAsync(request);
}
catch (BentenException ex) when (ex.ErrorCode == "BSC4076")
{
    Console.WriteLine("The Benten API key is invalid or has been revoked.");
}
```

---

## Error codes

Benten API uses `BSCxxxx` status codes in error responses. The SDK puts the code in `ErrorCode` and a description in `ErrorMessage`. Use `BentenStatusCodes.Describe(code)` to look up any code. Common codes:

| Code | Meaning |
|------|---------|
| `BSC4019` | Invalid access token (the SDK gets a new one and retries once) |
| `BSC4021` | User account is inactive |
| `BSC4022` | No registered device for this phone number |
| `BSC4032` | Account not active |
| `BSC4048` | Security key unavailable — contact Benten support |
| `BSC4056` | Device is not in Active state |
| `BSC4073` | Invalid request: the server could not read the request message |
| `BSC4074` | Request message is too large |
| `BSC4075` | API key is required |
| `BSC4076` | API key is invalid or has been revoked |
| `NoResponse` | User did not respond within 60 seconds |

---

## Companion repositories

- **Benten App** — the official iOS/Android app your users install from the [App Store](https://apps.apple.com/us/app/benten-app/id1508036928) or [Google Play](https://play.google.com/store/apps/details?id=com.bententechnologies.maui.bentenapp). Its source code is not public, but is available on request under a proprietary license. Contact [support@bententechnologies.com](mailto:support@bententechnologies.com).
- **[benten-client-sample](https://github.com/bentenauth/benten-client-sample)** — A working Windows Forms sample demonstrating all SDK operations.

---

## License

[MIT](LICENSE)
