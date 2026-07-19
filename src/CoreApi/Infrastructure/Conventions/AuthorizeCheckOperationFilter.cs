using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CoreApi.Infrastructure.Conventions;

/// <summary>
/// Adds the Bearer security requirement (padlock icon) only to operations whose controller
/// or action carries [Authorize], so Swagger UI doesn't misrepresent unprotected endpoints
/// (e.g. /health) as requiring a token.
/// </summary>
public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authorizeAttributes = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>()
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>() ?? [])
            .ToList();

        bool hasAllowAnonymous =
            context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        if (authorizeAttributes.Count == 0 || hasAllowAnonymous)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", context.Document), [] }
        });

        var requiredScopes = authorizeAttributes
            .Select(a => a.Policy)
            .Where(policy => !string.IsNullOrEmpty(policy))
            .ToList();

        if (requiredScopes.Count > 0)
        {
            string note = $"Requires scope: `{string.Join("`, `", requiredScopes)}`.";
            operation.Description = string.IsNullOrEmpty(operation.Description)
                ? note
                : $"{operation.Description}\n\n{note}";
        }
    }
}
