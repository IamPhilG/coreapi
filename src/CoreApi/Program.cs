using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Conventions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    // Transforms [controller] tokens to kebab-case: ServiceAccounts → service-accounts
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new() { Title = "CoreApi", Version = "v1" }));

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Default scheme declared now so [Authorize] on BaseApiController resolves implicitly.
// JWT Bearer validation is fully wired in Spec 3.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
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
