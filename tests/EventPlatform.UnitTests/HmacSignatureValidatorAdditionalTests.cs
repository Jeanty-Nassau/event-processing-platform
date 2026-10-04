using System.Security.Cryptography;
using System.Text;
using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Security;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace EventPlatform.UnitTests;

public class HmacSignatureValidatorAdditionalTests
{
    [Test]
    public void Validate_ReturnsFalseForEmptySignature()
    {
        var validator = CreateValidator("secret");

        var result = validator.Validate("{\"test\":true}", string.Empty);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_ReturnsFalseForWrongSecret()
    {
        var payload = "{\"test\":true}";
        var validator = CreateValidator("secret-a");
        var signature = CreateSignature(payload, "secret-b");

        var result = validator.Validate(payload, signature);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_ReturnsFalseForInvalidHexLength()
    {
        var validator = CreateValidator("secret");

        var result = validator.Validate("{\"test\":true}", "sha256=abcd");

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_ReturnsFalseForNonHexCharacters()
    {
        var validator = CreateValidator("secret");

        var result = validator.Validate("{\"test\":true}", "sha256=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz");

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_ReturnsFalseForOneByteMutation()
    {
        const string payload = "{\"test\":true}";
        var secret = "secret";
        var validSignature = CreateSignature(payload, secret);
        var mutated = validSignature.Substring(0, validSignature.Length - 1) + (validSignature[^1] == '0' ? '1' : '0');
        var validator = CreateValidator(secret);

        var result = validator.Validate(payload, mutated);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_AllowsRawSpanPath()
    {
        const string payload = "{\"test\":true}";
        const string secret = "secret";
        var signature = CreateSignature(payload, secret);
        var validator = CreateValidator(secret);

        var bytes = Encoding.UTF8.GetBytes(payload);
        var result = validator.IsValid(bytes, signature);

        Assert.That(result, Is.True);
    }

    private static HmacSignatureValidator CreateValidator(string secret)
    {
        return new HmacSignatureValidator(Options.Create(new EventSecurityOptions { SigningSecret = secret }));
    }

    private static string CreateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
