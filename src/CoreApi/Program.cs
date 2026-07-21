using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Authorization;
using CoreApi.Infrastructure.Conventions;
using CoreApi.Infrastructure.Observability;
using CoreApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. LogLevel filters still come from the "Logging" configuration section; only
// the sink shape changes by environment: a human-readable single-object console in Development, a
// machine-parseable JSON console (stdout) elsewhere -- which any collector (CloudWatch, Datadog,
// Splunk, ...) can ingest without CoreApi taking a dependency on one. Scopes are included so the
// per-request correlation id rides on every line. No file sink: secrets are never written to disk.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.SingleLine = false;
        options.TimestampFormat = "HH:mm:ss ";
    });
else
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers(options =>
{
    // Transforms [controller] tokens to kebab-case: ServiceAccounts → service-accounts
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
    // Records list-response sizes so the request log can report results=N.
    options.Filters.Add<ResultCountFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CoreApi", Version = "v1" });
    // GroupName on [ApiExplorerSettings] (Users, ServiceAccounts, ...) is a display tag, not a
    // separate document -- there is only one Swagger document ("v1"). Swashbuckle's default
    // DocInclusionPredicate treats GroupName as a document selector and silently drops every
    // action from "v1" otherwise, since no GroupName equals "v1".
    options.DocInclusionPredicate((_, _) => true);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT access token — Swagger UI adds the \"Bearer \" prefix automatically."
    });
    string xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // Registered AFTER IncludeXmlComments so the "Requires scope" note is appended to the
    // XML-derived description rather than overwritten by it (operation filters run in
    // registration order). Endpoints without XML remarks still get the note alone.
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
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
        // Without this, the handler silently remaps short claim types (e.g. "scp", "sub") to
        // legacy long-form URIs (e.g. "http://schemas.microsoft.com/identity/claims/scope"),
        // which breaks any code -- like ScopePolicies.HasScope -- that reads the claim type
        // the IdP actually issued.
        options.MapInboundClaims = false;
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

builder.Services.AddAuthorization(options =>
{
    foreach (string scope in new[]
             {
                 ScopePolicies.UsersRead, ScopePolicies.UsersCreate,
                 ScopePolicies.UsersUpdate, ScopePolicies.UsersDelete,
                 ScopePolicies.GroupsRead,
             })
    {
        options.AddPolicy(scope, policy => policy.RequireAssertion(ctx => ScopePolicies.HasScope(ctx.User, scope)));
    }
});

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

// Keyed pseudonymisation for operational logs (SubjectFingerprint / ObjectFingerprint). The HMAC
// key is required outside Development/Test; there is deliberately no silent production fallback.
bool pseudonymizationKeyOptional =
    builder.Environment.IsDevelopment()
    || string.Equals(builder.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase);

builder.Services.AddOptions<ObservabilityOptions>()
    .BindConfiguration(ObservabilityOptions.SectionName)
    .Validate(
        opt => pseudonymizationKeyOptional || opt.HasValidPseudonymizationKey,
        $"Observability:PseudonymizationKey is required and must be at least {HmacPseudonymizer.MinimumKeyBytes} bytes outside Development/Test.")
    .ValidateOnStart();

builder.Services.AddSingleton<IPseudonymizer>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
    byte[] key = opt.HasValidPseudonymizationKey
        ? Encoding.UTF8.GetBytes(opt.PseudonymizationKey!)
        // Development/Test only: a fixed, clearly non-secret key so the service works without
        // configuration. Validation above guarantees a real key is present everywhere else.
        : Encoding.UTF8.GetBytes("coreapi-development-only-pseudonymization-key-not-a-secret-value");
    return new HmacPseudonymizer(key);
});

builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>();
builder.Services.AddScoped<IUserService, UserService>();

// Minimal single-node rate limiting (COREAPI-02). Enabled by default outside Development so
// local Swagger and local tests are never throttled; the switch and limits are configurable.
builder.Services.AddOptions<RateLimitOptions>()
    .BindConfiguration(RateLimitOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var rateLimitOptions =
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
bool rateLimitingEnabled = rateLimitOptions.Enabled ?? !builder.Environment.IsDevelopment();

if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(limiter =>
    {
        // Fail fast with 429 rather than absorbing bursts behind a queue.
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                ResolveRateLimitPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                    QueueLimit = rateLimitOptions.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));
    });
}

var app = builder.Build();

// Outermost so its correlation-id logging scope wraps the exception handler too, and so the
// request-completed log observes the final (handler-adjusted) status code.
app.UseMiddleware<RequestObservabilityMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages(); // Formats non-exception 4xx/5xx as RFC 7807 ProblemDetails bodies.

// Swagger/OpenAPI is a Development-only surface: the endpoints themselves are never mapped
// outside Development, so /swagger/* returns 404 in Production rather than being hidden visually.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoreApi v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
// After authentication so the limiter can partition by the authenticated identity when present.
if (rateLimitingEnabled)
    app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// Liveness/readiness probes must never be throttled: exclude /health from the global limiter
// so a burst of probes (or unrelated traffic sharing its partition) can't turn it into a 429.
app.MapHealthChecks("/health")
    .DisableRateLimiting();

// Structured lifecycle events, so a log stream shows exactly when an instance became ready and
// when it began draining.
app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation(ObservabilityEvents.ApplicationStarted, "CoreApi started in {Environment}", app.Environment.EnvironmentName));
app.Lifetime.ApplicationStopping.Register(() =>
    app.Logger.LogInformation(ObservabilityEvents.ApplicationStopping, "CoreApi stopping in {Environment}", app.Environment.EnvironmentName));

app.Run();

// Partitions rate-limit buckets by, in order of preference: a stable authenticated identity,
// then the client IP, then an explicit shared fallback when neither is available.
static string ResolveRateLimitPartitionKey(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        string? subject = context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(subject))
            return $"user:{subject}";
    }

    string? ip = context.Connection.RemoteIpAddress?.ToString();
    return string.IsNullOrEmpty(ip) ? "anonymous" : $"ip:{ip}";
}

// Exposes the top-level Program class to WebApplicationFactory<Program> in the test projects.
public partial class Program;
