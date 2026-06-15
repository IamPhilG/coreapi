using CoreApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new() { Title = "CoreApi", Version = "v1" }));

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// JWT authentication wired in Spec 3
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// AD DS connection layer (Spec 2)
builder.Services.AddOptions<DirectoryConnectionOptions>()
    .BindConfiguration(DirectoryConnectionOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoreApi v1"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
