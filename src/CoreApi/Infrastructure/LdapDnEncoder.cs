using System.Text;

namespace CoreApi.Infrastructure;

/// <summary>
/// Escapes a value for safe inclusion as an RDN component in a distinguished name (RFC 4514).
/// A different rule set from <see cref="LdapFilterEncoder"/> -- filter escaping protects a
/// search filter, this protects DN structure. Use on every user-supplied value composed into
/// a DN (e.g. the CN of a new object), never interchangeably with filter escaping.
/// </summary>
public static class LdapDnEncoder
{
    public static string EscapeRdnValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var sb = new StringBuilder(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isFirst = i == 0;
            bool isLast = i == value.Length - 1;

            if (c is ',' or '+' or '"' or '\\' or '<' or '>' or ';' or '=')
                sb.Append('\\').Append(c);
            else if (isFirst && c == '#')
                sb.Append("\\#");
            else if ((isFirst || isLast) && c == ' ')
                sb.Append("\\ ");
            else if (c == '\0')
                sb.Append("\\00");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
