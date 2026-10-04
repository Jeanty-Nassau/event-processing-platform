using System.Security.Cryptography;
using System.Text;
using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Security;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace EventPlatform.UnitTests;

public class HmacSignatureValidatorTests
{
    [Test]
    public void Validate_ReturnsTrueForValidSignature()
    {
        const string payload = "{\"eventType\":\"demo.work.requested\"}";
        const string secret = "super-secret";
        var signature = CreateSignature(payload, secret);
        var validator = CreateValidator(secret);

        var result = validator.Validate(payload, signature);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Validate_ReturnsFalseForWrongPayload()
    {
        const string payload = "{\"eventType\":\"demo.work.requested\"}";
        const string secret = "super-secret";
        var signature = CreateSignature(payload, secret);
        var validator = CreateValidator(secret);

        var result = validator.Validate("{\"eventType\":\"demo.work.completed\"}", signature);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_ReturnsFalseForMalformedSignaturePrefix()
    {
        const string payload = "{\"eventType\":\"demo.work.requested\"}";
        var validator = CreateValidator("super-secret");

        var result = validator.Validate(payload, "md5=abc123");

        Assert.That(result, Is.False);
    }

    [Test]
    public void Constructor_ThrowsWhenSecretMissing()
    {
        var options = Options.Create(new EventSecurityOptions());

        var exception = Assert.Throws<InvalidOperationException>(() => new HmacSignatureValidator(options));

        Assert.That(exception!.Message, Does.Contain("SigningSecret"));
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
