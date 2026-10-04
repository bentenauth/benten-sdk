// ============================================================================
// This file is part of the Benten Authentication and Authorization Platform.
// Copyright (c) 2024 Benten Technologies.
//
// Licensed under the MIT License. See LICENSE in the repository root for full
// license text.
// ============================================================================

using System.Text.RegularExpressions;

namespace Benten.SDK;

/// <summary>
/// Benten status codes (<c>BSCxxxx</c>) returned by the Benten API, with
/// human-readable descriptions.
/// </summary>
public static class BentenStatusCodes
{
    /// <summary>The user denied the request (returned by some token flows instead of <c>Deny</c>).</summary>
    public const string RequestDeniedByUser = "BSC4068";

    private static readonly Regex CodePattern =
        new(@"^BSC\d{4}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EmbeddedCodePattern =
        new(@"\bBSC\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BSC4001"] = "Admin account is not active.",
            ["BSC4002"] = "Username and/or password is incorrect.",
            ["BSC4005"] = "Username already exists.",
            ["BSC4006"] = "Failed to create user. Contact Benten support.",
            ["BSC4007"] = "Unauthorized: username and/or password is incorrect.",
            ["BSC4008"] = "Username not found.",
            ["BSC4017"] = "Account was created, but the activation email could not be sent. Contact Benten support.",
            ["BSC4019"] = "Invalid token.",
            ["BSC4020"] = "No registered user found for the given client ID.",
            ["BSC4021"] = "Client account is inactive. Contact Benten support.",
            ["BSC4022"] = "No registered device found for the given country and phone number.",
            ["BSC4023"] = "An account already exists for the given phone number.",
            ["BSC4024"] = "Failed to create the online account.",
            ["BSC4026"] = "Card number/zip code already exists.",
            ["BSC4027"] = "Failed to create the credit card record.",
            ["BSC4029"] = "IoT device is already associated with the given country and phone number.",
            ["BSC4030"] = "Failed to create the IoT device record.",
            ["BSC4032"] = "Account is inactive. Contact Benten support.",
            ["BSC4048"] = "Security key unavailable. Contact Benten support.",
            ["BSC4049"] = "Invalid token: missing client ID.",
            ["BSC4051"] = "Credit card type not supported. Contact Benten support.",
            ["BSC4052"] = "Bearer token missing.",
            ["BSC4053"] = "Required user data is missing.",
            ["BSC4056"] = "Device is not active. Activate the device first.",
            ["BSC4057"] = "No login account associated with the client was found for this device.",
            ["BSC4058"] = "Client is not authorized to request login permission for this device.",
            ["BSC4059"] = "System currently unavailable. Contact Benten support if this persists.",
            ["BSC4060"] = "Invalid Benten credit card ID.",
            ["BSC4061"] = "Unauthorized credit card transaction request.",
            ["BSC4062"] = "No IoT device found for the given name and MAC address.",
            ["BSC4063"] = "Unauthorized IoT setting change request.",
            ["BSC4064"] = "No devices configured for the given IoT device.",
            ["BSC4065"] = "Default Benten account unavailable. Contact Benten support.",
            ["BSC4066"] = "Default Benten admin account unavailable. Contact Benten support.",
            ["BSC4067"] = "Phone number not found.",
            ["BSC4068"] = "Request denied by user.",
            ["BSC4069"] = "Token security key unavailable. Contact Benten support.",
            ["BSC5000"] = "Internal server error. Try again later.",
        };

    /// <summary>
    /// Returns true if <paramref name="value"/> is a Benten status code such as <c>BSC4022</c>.
    /// </summary>
    public static bool IsStatusCode(string? value) =>
        !string.IsNullOrWhiteSpace(value) && CodePattern.IsMatch(value.Trim());

    /// <summary>
    /// Returns a human-readable description of a Benten status code.
    /// Unknown codes return a generic message that includes the code.
    /// </summary>
    public static string Describe(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        code = code.Trim();
        return Descriptions.TryGetValue(code, out var description)
            ? description
            : $"Benten status code {code.ToUpperInvariant()}. Contact Benten support if this persists.";
    }

    /// <summary>
    /// Finds a status code inside a longer message, e.g. "BSC4019 - Invalid Token".
    /// Returns an empty string if the message contains no code.
    /// </summary>
    internal static string FindCode(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var match = EmbeddedCodePattern.Match(message);
        return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
    }
}
