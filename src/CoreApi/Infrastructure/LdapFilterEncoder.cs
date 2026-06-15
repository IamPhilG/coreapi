using System.Text;

namespace CoreApi.Infrastructure;

/// <summary>
/// Escapes values for safe inclusion in LDAP search filter expressions (RFC 4515).
/// Use this on every user-supplied value before composing it into a filter string.
/// </summary>
public static class LdapFilterEncoder
{
    public static string? Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder(value.Length + 4);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*':  sb.Append("\\2a"); break;
                case '(':  sb.Append("\\28"); break;
                case ')':  sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default:   sb.Append(ch);    break;
            }
        }
        return sb.ToString();
    }
}
