using System.Security.Cryptography;
using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Conventions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    // Transforms [controller] tokens to kebab-case: ServiceAccounts → service-accounts
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CoreApi", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT access token — Swagger UI adds the \"Bearer \" prefix automatically."
    });
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Jwt:Authority/Audience/Issuer are always required (fail-fast), even in Development where
// DevSigningKeyPath bypasses the live Authority metadata fetch below.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(
        opt => builder.Environment.IsDevelopment() || string.IsNullOrEmpty(opt.DevSigningKeyPath),
        "Jwt:DevSigningKeyPath must not be set outside Development.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = jwtOptions.ValidAlgorithms,
        };

        if (builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(jwtOptions.DevSigningKeyPath))
        {
            // Dev-only: validate against a local static RSA public key instead of fetching
            // JWKS from a live Authority, so local tokens can be minted and verified with no
            // external IdP dependency. RSAParameters is a value-type snapshot, safe to use
            // after the RSA handle is disposed.
            // Resolve relative to ContentRootPath (the project directory), not the process's
            // current directory, which varies depending on how `dotnet run`/`dotnet test` is invoked.
            string keyPath = Path.IsPathRooted(jwtOptions.DevSigningKeyPath)
                ? jwtOptions.DevSigningKeyPath
                : Path.Combine(builder.Environment.ContentRootPath, jwtOptions.DevSigningKeyPath);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false));
        }
        else
        {
            options.Authority = jwtOptions.Authority;
        }
    });

builder.Services.AddAuthorization();

// AD DS connection layer (Spec 2)
builder.Services.AddOptions<DirectoryConnectionOptions>()
    .BindConfiguration(DirectoryConnectionOptions.SectionName)
    .ValidateDataAnnotations()
    // ServiceAccountPassword is required whenever ServiceAccountUser is set.
    .Validate(
        opt => string.IsNullOrEmpty(opt.ServiceAccountUser) || !string.IsNullOrEmpty(opt.ServiceAccountPassword),
        "DirectoryConnection:ServiceAccountPassword is required when ServiceAccountUser is set.")
    // LDAPS must be enforced in every non-Development environment (security requirement).
    .Validate(
        opt => builder.Environment.IsDevelopment() || opt.UseTls,
        "DirectoryConnection:UseTls must be true in non-Development environments.")
    .ValidateOnStart();

builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // Formats non-exception 4xx/5xx as RFC 7807 ProblemDetails bodies.
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoreApi v1"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
