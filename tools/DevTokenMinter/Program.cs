// Dev-only tool: mints JWTs signed with a local RSA keypair so Spec 3's JWT Bearer
// middleware can be exercised (Swagger UI Authorize button, curl) without a live IdP.
// Never used outside Development -- CoreApi's own Program.cs refuses DevSigningKeyPath
// in any other environment.
//
// Usage: dotnet run -- [profile] [issuer] [audience]
//   profile: valid | expired | wrong-audience | wrong-issuer | unsigned | tampered (default: valid)
//
// First run generates dev-signing-key.private.pem / .public.pem next to this file
// (gitignored). Point CoreApi's appsettings.Development.json Jwt:DevSigningKeyPath at the
// .public.pem file.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

string projectDir = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(projectDir, "DevTokenMinter.csproj")))
{
    string? parent = Path.GetDirectoryName(projectDir.TrimEnd(Path.DirectorySeparatorChar));
    if (parent is null)
        throw new InvalidOperationException("Could not locate DevTokenMinter.csproj above the build output directory.");
    projectDir = parent;
}

string privateKeyPath = Path.Combine(projectDir, "dev-signing-key.private.pem");
string publicKeyPath = Path.Combine(projectDir, "dev-signing-key.public.pem");

if (!File.Exists(privateKeyPath))
{
    using var generated = RSA.Create(2048);
    File.WriteAllText(privateKeyPath, generated.ExportRSAPrivateKeyPem());
    File.WriteAllText(publicKeyPath, generated.ExportRSAPublicKeyPem());
    Console.Error.WriteLine($"[DevTokenMinter] Generated new dev signing key pair in {projectDir}");
    Console.Error.WriteLine($"[DevTokenMinter] Point Jwt:DevSigningKeyPath at: {publicKeyPath}");
}

using var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

string profile = args.Length > 0 ? args[0] : "valid";
string issuer = args.Length > 1 ? args[1] : "https://dev-sts.coreapi.local";
string audience = args.Length > 2 ? args[2] : "coreapi";

var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, "dev-caller") };
var handler = new JwtSecurityTokenHandler();
var now = DateTime.UtcNow;

JwtSecurityToken Build(string useIssuer, string useAudience, DateTime notBefore, DateTime expires, SigningCredentials? creds) =>
    new(useIssuer, useAudience, claims, notBefore, expires, creds);

string tokenString = profile switch
{
    "valid" => handler.WriteToken(Build(issuer, audience, now, now.AddHours(1), signingCredentials)),
    "expired" => handler.WriteToken(Build(issuer, audience, now.AddHours(-2), now.AddHours(-1), signingCredentials)),
    "wrong-audience" => handler.WriteToken(Build(issuer, "some-other-api", now, now.AddHours(1), signingCredentials)),
    "wrong-issuer" => handler.WriteToken(Build("https://not-the-configured-issuer.example", audience, now, now.AddHours(1), signingCredentials)),
    "unsigned" => handler.WriteToken(Build(issuer, audience, now, now.AddHours(1), null)), // alg: none
    "tampered" => Tamper(handler.WriteToken(Build(issuer, audience, now, now.AddHours(1), signingCredentials))),
    _ => throw new ArgumentException($"Unknown profile '{profile}'. Use: valid | expired | wrong-audience | wrong-issuer | unsigned | tampered"),
};

Console.WriteLine(tokenString);

static string Tamper(string jwt)
{
    // Flip the last character of the signature segment so the signature no longer matches.
    string[] parts = jwt.Split('.');
    char last = parts[2][^1];
    char replacement = last == 'A' ? 'B' : 'A';
    parts[2] = parts[2][..^1] + replacement;
    return string.Join('.', parts);
}
