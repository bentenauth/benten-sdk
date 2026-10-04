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
- A Benten developer account (contact [support@bententechnologies.com](mailto:support@bententechnologies.com))
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

var benten = new BentenClient("wss://api.bententechnologies.com/v2/");

// 1. Obtain a Bearer token with your Benten developer credentials
string jwt = await benten.GetBearerTokenAsync("your-username", "your-password");

// 2. Request login approval for a user
var result = await benten.RequestLoginApprovalAsync(jwt, new LoginRequest
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

## API reference

### `BentenClient(string serverBaseUrl)`

Creates a client pointing at the Benten API server.

| Parameter | Description |
|-----------|-------------|
| `serverBaseUrl` | Base URL, e.g. `wss://api.bententechnologies.com/v2/`. The SDK handles wss↔https conversion automatically, and appends `/v2` if the URL has no version segment. Use `new BentenClient()` to target production without passing a URL. |

---

### `GetBearerTokenAsync(username, password, timeoutSeconds?)`

Authenticates with your Benten developer credentials and returns a JWT Bearer token. Supply this token to all subsequent approval methods.

```csharp
string jwt = await benten.GetBearerTokenAsync("myuser", "mypass");
```

---

### `RequestLoginApprovalAsync(jwt, LoginRequest, cancellationToken?)`

Requests user approval for a login attempt.

```csharp
var result = await benten.RequestLoginApprovalAsync(jwt, new LoginRequest
{
    Country     = "United States",
    PhoneNumber = "6501234567",
    Username    = "alice@example.com"
});
```

---

### `RequestCCTransactionApprovalAsync(jwt, CCTransactionRequest, cancellationToken?)`

Requests user approval for a credit card transaction.

```csharp
var result = await benten.RequestCCTransactionApprovalAsync(jwt, new CCTransactionRequest
{
    CreditCardId = "card-id-from-benten",  // Issued when the card is registered
    Amount       = "$149.99",
    Retailer     = "Home Depot"
});
```

---

### `RequestIoTSettingApprovalAsync(jwt, IoTSettingRequest, cancellationToken?)`

Requests user approval for an IoT device configuration change.

```csharp
var result = await benten.RequestIoTSettingApprovalAsync(jwt, new IoTSettingRequest
{
    Name          = "Living Room Thermostat",
    MacAddress    = "AA:BB:CC:DD:EE:01",
    SettingChange = "Change temperature from 72°F to 68°F"
});
```

---

### `RequestGenericNotificationApprovalAsync(jwt, GenericNotificationRequest, cancellationToken?)`

Requests user approval for any custom action via a three-field notification.

```csharp
var result = await benten.RequestGenericNotificationApprovalAsync(jwt, new GenericNotificationRequest
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

### `RequestAuthTokenAsync(AuthTokenRequest, cancellationToken?)`

Requests a Benten-issued JWT for a user — confirmed by the user tapping Allow on their phone. The token is returned only after explicit user consent. No password required.

```csharp
var tokenResult = await benten.RequestAuthTokenAsync(new AuthTokenRequest
{
    Country     = "United States",
    PhoneNumber = "6501234567"
});

if (tokenResult.IsSuccess)
    Console.WriteLine($"Token: {tokenResult.Token}");
else if (tokenResult.HasError)
    Console.WriteLine($"Error: {tokenResult.Error}");
```

`AuthTokenResult` has the same `Outcome`, `ErrorCode`, `ErrorMessage` and `Error` properties as `BentenResult`, plus `Token`, `Country` and `PhoneNumber`. `IsSuccess` is true only when the user approved and a token was issued.

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
| `RequestId` | The request ID (`_id`) echoed by the server |
| `Response` | Raw `Response` value from the server ("Allow", "Deny", "NoResponse", or a status code) |
| `RawResponse` | Full string received from the server |

### Errors: returned vs. thrown

- **Returned:** errors reported by the Benten API for an approval request (invalid token, unknown phone number, inactive device…) come back in the result with `Outcome == ApprovalOutcome.Error`. Nothing is thrown.
- **Thrown as `BentenException`:** problems talking to the Benten API, and a failed `GetBearerTokenAsync`:
  - the connection can't be opened within 15 seconds, or fails partway through;
  - the server closes the connection without replying (the message includes its reason, and `ErrorCode` holds any status code in it);
  - no reply arrives within 75 seconds (the server itself replies `NoResponse` after 60 seconds, so this means the server or network is in trouble);
  - for a failed token request, `ErrorCode` holds the status code (e.g. `BSC4002` for a wrong password) and `StatusCode` holds the HTTP status.
- **Thrown as `OperationCanceledException`:** you cancelled the request through the `CancellationToken` you passed in. This is never reported as a timeout, so you can tell the two apart.

```csharp
try
{
    string jwt = await benten.GetBearerTokenAsync(username, password);
}
catch (BentenException ex) when (ex.ErrorCode == "BSC4002")
{
    Console.WriteLine("Wrong developer username or password.");
}
```

---

## Error codes

Benten API uses `BSCxxxx` status codes in error responses. The SDK puts the code in `ErrorCode` and a description in `ErrorMessage`. Use `BentenStatusCodes.Describe(code)` to look up any code. Common codes:

| Code | Meaning |
|------|---------|
| `BSC4002` | Invalid credentials |
| `BSC4008` | Application user not found |
| `BSC4021` | User account is inactive |
| `BSC4022` | No registered device for this phone number |
| `BSC4032` | Account not active |
| `BSC4048` | Security key unavailable — contact Benten support |
| `BSC4056` | Device is not in Active state |
| `BSC4067` | Phone number not found |
| `BSC4068` | Request denied by user |
| `BSC4069` | Token security key unavailable |
| `NoResponse` | User did not respond within 60 seconds |

---

## Companion repositories

- **Benten App** — the official iOS/Android app your users install from the [App Store](https://apps.apple.com/us/app/benten-app/id1508036928) or [Google Play](https://play.google.com/store/apps/details?id=com.bententechnologies.maui.bentenapp). Its source code is not public, but is available on request under a proprietary license. Contact [support@bententechnologies.com](mailto:support@bententechnologies.com).
- **[benten-client-sample](https://github.com/bentenauth/benten-client-sample)** — A working Windows Forms sample demonstrating all SDK operations.

---

## License

[MIT](LICENSE)
