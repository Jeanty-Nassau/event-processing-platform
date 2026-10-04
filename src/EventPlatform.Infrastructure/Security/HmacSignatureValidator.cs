using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EventPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EventPlatform.Infrastructure.Security;

public sealed class HmacSignatureValidator
{
    private const string Prefix = "sha256=";
    private readonly byte[] _secret;

    public HmacSignatureValidator(IOptions<EventSecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Value.SigningSecret))
        {
            throw new InvalidOperationException("EventSecurity:SigningSecret must be configured.");
        }

        _secret = Encoding.UTF8.GetBytes(options.Value.SigningSecret);
    }

    public bool IsValid(ReadOnlySpan<byte> payload, string suppliedSignature)
    {
        if (string.IsNullOrWhiteSpace(suppliedSignature))
        {
            return false;
        }

        if (!suppliedSignature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hex = suppliedSignature[Prefix.Length..];
        if (hex.Length != 64)
        {
            return false;
        }

        Span<byte> supplied = stackalloc byte[32];
        if (!TryDecodeHex(hex, supplied, out var written) || written != 32)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[32];
        using var hmac = new HMACSHA256(_secret);
        hmac.TryComputeHash(payload, expected, out _);

        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    public bool Validate(string payload, string suppliedSignature)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        return IsValid(Encoding.UTF8.GetBytes(payload), suppliedSignature);
    }

    private static bool TryDecodeHex(string hex, Span<byte> destination, out int written)
    {
        written = 0;

        if (hex.Length % 2 != 0)
        {
            return false;
        }

        for (var i = 0; i < hex.Length; i += 2)
        {
            if (written >= destination.Length)
            {
                return false;
            }

            var segment = hex.Substring(i, 2);
            if (!byte.TryParse(segment, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            destination[written++] = value;
        }

        return true;
    }
}
