using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure;
using CoreApi.Models;
using Microsoft.Extensions.Options;

namespace CoreApi.Services;

public sealed class UserService(
    IDirectoryConnection connection,
    IOptions<DirectoryConnectionOptions> directoryOptions) : IUserService
{
    private const string UserFilterBase = "(&(objectClass=user)(objectCategory=person)";
    private const int AccountDisabled = 0x0002;
    private const int NormalAccount = 0x0200;

    public async Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default)
    {
        var entry = await FindEntryAsync(samAccountName, cancellationToken);
        return MapToDto(entry);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? ouPath, int pageSize, CancellationToken cancellationToken = default)
    {
        string baseDn = string.IsNullOrEmpty(ouPath) ? directoryOptions.Value.BaseDn : ouPath;
        EnsureWithinConfiguredBaseDn(baseDn);

        var entries = await connection.SearchAsync(
            baseDn,
            $"{UserFilterBase})",
            SearchScope.Subtree,
            attributes: UserAttributes,
            maxResults: pageSize,
            cancellationToken: cancellationToken);

        return entries.Select(MapToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureWithinConfiguredBaseDn(request.OuPath);

        // RFC 4514 RDN escaping -- a different rule set from LdapFilterEncoder (which protects
        // search filters, not DN structure). Without it, a DisplayName containing a comma,
        // backslash, or leading '#' would break out of the intended RDN.
        string cn = LdapDnEncoder.EscapeRdnValue(
            string.IsNullOrEmpty(request.DisplayName) ? request.SamAccountName : request.DisplayName);
        string dn = $"CN={cn},{request.OuPath}";

        var attributes = new List<DirectoryAttribute>
        {
            new("objectClass", "user"),
            new("sAMAccountName", request.SamAccountName),
            new("userPrincipalName", request.UserPrincipalName),
            // Always created disabled: setting an initial password (unicodePwd) requires
            // LDAPS, which isn't configured on the test DC yet. Enabling is a separate,
            // currently unimplemented, follow-up that will need that LDAPS path.
            new("userAccountControl", (NormalAccount | AccountDisabled).ToString()),
        };
        AddIfPresent(attributes, "displayName", request.DisplayName);
        AddIfPresent(attributes, "givenName", request.GivenName);
        AddIfPresent(attributes, "sn", request.Surname);
        AddIfPresent(attributes, "mail", request.EmailAddress);
        AddIfPresent(attributes, "title", request.JobTitle);
        AddIfPresent(attributes, "department", request.Department);
        AddIfPresent(attributes, "manager", request.Manager);
        AddIfPresent(attributes, "description", request.Description);

        try
        {
            await connection.AddAsync(new AddRequest(dn, [.. attributes]), cancellationToken);
        }
        catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.EntryAlreadyExists)
        {
            // Message intentionally doesn't echo the constructed DN or OU path -- doing so
            // would let a caller map out the domain's container structure via collisions.
            throw new ConflictException($"A user '{request.SamAccountName}' already exists.");
        }

        return await GetBySamAccountNameAsync(request.SamAccountName, cancellationToken);
    }

    public async Task<UserDto> UpdateAsync(string samAccountName, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await FindEntryAsync(samAccountName, cancellationToken);

        var modifications = new List<DirectoryAttributeModification>();
        AddModificationIfPresent(modifications, "displayName", request.DisplayName);
        AddModificationIfPresent(modifications, "givenName", request.GivenName);
        AddModificationIfPresent(modifications, "sn", request.Surname);
        AddModificationIfPresent(modifications, "mail", request.EmailAddress);
        AddModificationIfPresent(modifications, "title", request.JobTitle);
        AddModificationIfPresent(modifications, "department", request.Department);
        AddModificationIfPresent(modifications, "manager", request.Manager);
        AddModificationIfPresent(modifications, "description", request.Description);

        if (modifications.Count > 0)
            await connection.ModifyAsync(
                new ModifyRequest(entry.DistinguishedName, [.. modifications]), cancellationToken);

        return await GetBySamAccountNameAsync(samAccountName, cancellationToken);
    }

    public async Task DeleteAsync(string samAccountName, CancellationToken cancellationToken = default)
    {
        var entry = await FindEntryAsync(samAccountName, cancellationToken);
        await connection.DeleteAsync(entry.DistinguishedName, cancellationToken);
    }

    /// <summary>Exposed for unit testing -- SearchResultEntry has no public constructor, so
    /// LDAP filter construction is tested in isolation from the result-mapping logic that
    /// requires a real search.</summary>
    internal static string BuildSamAccountNameFilter(string samAccountName) =>
        $"{UserFilterBase}(sAMAccountName={LdapFilterEncoder.Escape(samAccountName)}))";

    /// <summary>Parent container from a DN. Assumes the leading RDN has no unescaped comma in
    /// its value -- true for every field this service lets callers set. Exposed for unit
    /// testing (see <see cref="BuildSamAccountNameFilter"/> for why).</summary>
    internal static string ExtractOuPath(string distinguishedName) =>
        distinguishedName[(distinguishedName.IndexOf(',') + 1)..];

    /// <summary>Inverse of the userAccountControl ACCOUNTDISABLE bit. Exposed for unit testing
    /// (see <see cref="BuildSamAccountNameFilter"/> for why).</summary>
    internal static bool IsEnabled(int userAccountControl) => (userAccountControl & AccountDisabled) == 0;

    /// <summary>True when <paramref name="dn"/> is the configured base DN or a descendant of
    /// it. Without this check, a caller-supplied ouPath (Create/List) could target or search
    /// any container the service account can reach, regardless of what this API is meant to
    /// administer. Exposed for unit testing (see <see cref="BuildSamAccountNameFilter"/> for
    /// why).</summary>
    internal static bool IsDnWithinBaseDn(string dn, string baseDn) =>
        string.Equals(dn, baseDn, StringComparison.OrdinalIgnoreCase) ||
        dn.EndsWith("," + baseDn, StringComparison.OrdinalIgnoreCase);

    private void EnsureWithinConfiguredBaseDn(string dn)
    {
        string baseDn = directoryOptions.Value.BaseDn;
        if (!IsDnWithinBaseDn(dn, baseDn))
            throw new InvalidRequestException($"ouPath must be within the configured directory scope ('{baseDn}').");
    }

    private async Task<SearchResultEntry> FindEntryAsync(string samAccountName, CancellationToken cancellationToken)
    {
        string filter = BuildSamAccountNameFilter(samAccountName);
        var entries = await connection.SearchAsync(
            directoryOptions.Value.BaseDn,
            filter,
            SearchScope.Subtree,
            attributes: UserAttributes,
            cancellationToken: cancellationToken);

        return entries.FirstOrDefault()
            ?? throw new NotFoundException($"User '{samAccountName}' was not found.");
    }

    private static readonly string[] UserAttributes =
    [
        "objectGUID", "sAMAccountName", "userPrincipalName", "displayName", "givenName", "sn",
        "mail", "title", "department", "manager", "description", "distinguishedName",
        "userAccountControl",
    ];

    private static void AddIfPresent(List<DirectoryAttribute> attributes, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            attributes.Add(new DirectoryAttribute(name, value));
    }

    private static void AddModificationIfPresent(List<DirectoryAttributeModification> modifications, string name, string? value)
    {
        if (value is null)
            return;

        var mod = new DirectoryAttributeModification { Name = name, Operation = DirectoryAttributeOperation.Replace };
        mod.Add(value);
        modifications.Add(mod);
    }

    private static UserDto MapToDto(SearchResultEntry entry)
    {
        string dn = entry.DistinguishedName;
        int uac = int.Parse(GetSingle(entry, "userAccountControl") ?? "0");

        return new UserDto
        {
            Guid = new Guid((byte[])entry.Attributes["objectGUID"][0]).ToString(),
            SamAccountName = GetSingle(entry, "sAMAccountName") ?? string.Empty,
            UserPrincipalName = GetSingle(entry, "userPrincipalName") ?? string.Empty,
            DisplayName = GetSingle(entry, "displayName"),
            GivenName = GetSingle(entry, "givenName"),
            Surname = GetSingle(entry, "sn"),
            EmailAddress = GetSingle(entry, "mail"),
            JobTitle = GetSingle(entry, "title"),
            Department = GetSingle(entry, "department"),
            Manager = GetSingle(entry, "manager"),
            Description = GetSingle(entry, "description"),
            OuPath = ExtractOuPath(dn),
            Enabled = IsEnabled(uac),
        };
    }

    private static string? GetSingle(SearchResultEntry entry, string attributeName) =>
        entry.Attributes.Contains(attributeName) ? entry.Attributes[attributeName][0] as string : null;
}
