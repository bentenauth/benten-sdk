// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

namespace Benten.SDK.Models;

/// <summary>
/// Requests approval for an IoT device setting change.
/// Benten sends a push notification to the device owner's registered mobile;
/// the user taps Allow or Deny and the result is returned on the WebSocket.
/// <br/>
/// WebSocket endpoint: <c>clients/iotsetting/update</c> (relative to the <see cref="BentenClient"/> base URL)
/// </summary>
public class IoTSettingRequest : ClientRequest
{
    /// <summary>
    /// Initialises a new <see cref="IoTSettingRequest"/> with
    /// <see cref="ClientRequest.RequestType"/> set to <c>"IoTSettingChange"</c>.
    /// </summary>
    public IoTSettingRequest() : base("IoTSettingChange") { }

    /// <summary>
    /// A human-readable name for the IoT device (e.g. "Living Room Thermostat").
    /// Displayed on the user's notification.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The MAC address of the IoT device (e.g. "AA:BB:CC:DD:EE:01").
    /// Used to identify the device in the Benten system.
    /// </summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// A description of the setting change being requested
    /// (e.g. "Change temperature from 72°F to 68°F").
    /// Displayed on the user's notification.
    /// </summary>
    public string SettingChange { get; set; } = string.Empty;
}
