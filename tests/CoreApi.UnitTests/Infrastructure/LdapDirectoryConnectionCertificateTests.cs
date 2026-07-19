using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CoreApi.Infrastructure;

namespace CoreApi.UnitTests.Infrastructure;

/// <summary>
/// Covers CertificateHostnameMatches in isolation from the live TLS handshake it's normally
/// invoked from (see LdapDirectoryConnectionTests.cs, which sets UseTls=false to avoid needing
/// one). Certificates are generated in-memory, no network or real CA involved.
/// </summary>
public class LdapDirectoryConnectionCertificateTests
{
    private static X509Certificate2 SelfSignedCertificate(string sanDnsName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={sanDnsName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(sanDnsName);
        request.CertificateExtensions.Add(sanBuilder.Build());

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Matches_when_SAN_equals_expected_host()
    {
        using var cert = SelfSignedCertificate("dc01.corp.local");
        Assert.True(LdapDirectoryConnection.CertificateHostnameMatches(cert, "dc01.corp.local"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Matches_case_insensitively()
    {
        using var cert = SelfSignedCertificate("dc01.corp.local");
        Assert.True(LdapDirectoryConnection.CertificateHostnameMatches(cert, "DC01.CORP.LOCAL"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_a_certificate_issued_for_a_different_host()
    {
        // The scenario the missing check let through: a certificate that's perfectly valid and
        // chain-trusted, just for the wrong server -- the case a machine-in-the-middle would
        // present using a different, but still trusted, certificate.
        using var cert = SelfSignedCertificate("attacker.example.com");
        Assert.False(LdapDirectoryConnection.CertificateHostnameMatches(cert, "dc01.corp.local"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_a_subdomain_that_is_not_an_exact_match()
    {
        using var cert = SelfSignedCertificate("dc01.corp.local");
        Assert.False(LdapDirectoryConnection.CertificateHostnameMatches(cert, "evil.dc01.corp.local"));
    }
}
