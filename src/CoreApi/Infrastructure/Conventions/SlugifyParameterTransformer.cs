using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace CoreApi.Infrastructure.Conventions;

/// <summary>
/// Converts PascalCase controller / action names to kebab-case route segments.
/// ServiceAccounts → service-accounts, Users → users.
/// </summary>
public sealed class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    private static readonly Regex _camelBoundary =
        new(@"([a-z])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? TransformOutbound(object? value)
        => value is null
            ? null
            : _camelBoundary.Replace(value.ToString()!, "$1-$2").ToLowerInvariant();
}
