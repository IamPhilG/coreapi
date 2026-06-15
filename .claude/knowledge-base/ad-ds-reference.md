---
name: ad-ds-reference
description: Curated AD DS facts for code generation — attributes, LDAP syntax, ACL structure, protocol references
metadata:
  type: reference
---

# AD DS Reference

## Key protocol specifications

| Protocol | Code | What it covers | URL |
|----------|------|---------------|-----|
| Active Directory Technical Spec | MS-ADTS | Core AD schema, LDAP extensions, Kerberos integration, replication | https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/d2435927-0999-4c62-8c6d-13ba31a52e1a |
| SAM Remote Protocol | MS-SAMR | User, group, and computer account management operations | https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/4df07fab-1bbc-452f-8e92-7853a3c7e380 |
| Kerberos Protocol Extensions | MS-KILE | Windows-specific Kerberos extensions, PAC, S4U | https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-kile/2a32282e-dd48-4ad9-a542-609804b02cc9 |
| LSAD Remote Protocol | MS-LSAD | Local Security Authority domain policy, trust management | https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-lsad/1b5471ef-4c33-4a91-b079-dfcbb82f05cc |
| Authorization Protocols Overview | MS-AUTHSOD | How Windows evaluates access tokens and ACLs | https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-authsod/953992f8-4f5e-4f6a-a2d1-24d95d188d72 |
| Windows Protocols top level | MS-WINPROTLP | Index of all Windows protocols | https://learn.microsoft.com/en-us/openspecs/windows_protocols/MS-WINPROTLP/92b33e19-6fff-496b-86c3-d168206f9845 |

---

## Canonical LDAP attribute names

These are case-sensitive in filter construction and must match exactly.

### User attributes

| Attribute | Type | Notes |
|-----------|------|-------|
| `sAMAccountName` | String | Logon name (pre-Win2000). Max 20 chars. Unique per domain. |
| `userPrincipalName` | String | UPN: `user@domain.com`. Must be unique in forest. |
| `distinguishedName` | DN | Full path: `CN=John,OU=Users,DC=corp,DC=local`. Read-only via LDAP writes — set implicitly by placement. |
| `cn` | String | Common Name. Used as the RDN in the DN. |
| `displayName` | String | Display name in UI. |
| `givenName` | String | First name. |
| `sn` | String | Surname / last name. |
| `mail` | String | Email address. Not enforced unique by AD (only Exchange enforces uniqueness). |
| `telephoneNumber` | String | Primary phone. |
| `department` | String | Department. |
| `title` | String | Job title. |
| `manager` | DN | DN of the manager object. |
| `memberOf` | DN (multi) | Groups the user belongs to. **Read-only** — manage via group's `member` attribute. |
| `userAccountControl` | Integer | Bitmask controlling account state. See flags below. |
| `accountExpires` | LargeInteger | Windows FILETIME. 0 or `9223372036854775807` = never expires. |
| `pwdLastSet` | LargeInteger | Set to 0 to force password change; -1 to clear the force-change flag. |
| `objectSid` | SID | Security Identifier. Binary. Immutable once created. |
| `objectGUID` | GUID | Globally unique. Binary. Use for stable cross-rename references. |
| `objectClass` | String (multi) | Always includes `top`, `person`, `organizationalPerson`, `user` for user objects. |
| `whenCreated` | GeneralizedTime | UTC creation time. Format: `YYYYMMDDHHmmss.0Z` |
| `whenChanged` | GeneralizedTime | UTC last-modified time. |

### userAccountControl flags (most relevant)

| Flag | Value (hex) | Meaning |
|------|------------|---------|
| `ACCOUNTDISABLE` | 0x0002 | Account disabled |
| `HOMEDIR_REQUIRED` | 0x0008 | Home directory required |
| `LOCKOUT` | 0x0010 | Account locked out |
| `PASSWD_NOTREQD` | 0x0020 | No password required |
| `PASSWD_CANT_CHANGE` | 0x0040 | User cannot change password |
| `NORMAL_ACCOUNT` | 0x0200 | Standard user account (always set for users) |
| `DONT_EXPIRE_PASSWD` | 0x10000 | Password never expires |
| `SMARTCARD_REQUIRED` | 0x40000 | Smart card required for logon |
| `TRUSTED_FOR_DELEGATION` | 0x80000 | Kerberos unconstrained delegation |
| `NOT_DELEGATED` | 0x100000 | Account is sensitive, cannot be delegated |
| `PASSWORD_EXPIRED` | 0x800000 | Password has expired |

A normal enabled account = `0x200` (512 decimal). A normal disabled account = `0x202` (514).

### Service account attributes (differences from users)

Service accounts are user objects (`objectClass: user`) with specific conventions:
- Typically placed in a dedicated OU (e.g., `OU=ServiceAccounts,DC=corp,DC=local`)
- `servicePrincipalName` (multi-value): SPNs registered for Kerberos delegation. Format: `ServiceClass/FQDN:Port`
- `userAccountControl` typically includes `DONT_EXPIRE_PASSWD` (`0x10200`)
- `description` should document the owning application
- For gMSA: `objectClass` includes `msDS-GroupManagedServiceAccount`; password managed automatically by AD
- For sMSA: `objectClass` includes `msDS-ManagedServiceAccount`; bound to a single computer

### Group attributes

| Attribute | Notes |
|-----------|-------|
| `cn` | Group name |
| `sAMAccountName` | Pre-Win2000 name. Same value as `cn` typically. |
| `member` | Multi-value DN list of members. **Write here to add/remove members.** |
| `memberOf` | Groups this group belongs to (nesting). Read-only. |
| `groupType` | Bitmask: scope (local/global/universal) + type (security/distribution). See below. |
| `description` | Free-text description. |
| `mail` | Email for mail-enabled groups. |

### groupType bitmask values

| Type | Value (hex) | Notes |
|------|------------|-------|
| Global Security | 0x80000002 | Most common for user groups |
| Domain Local Security | 0x80000004 | Resource access groups |
| Universal Security | 0x80000008 | Cross-domain |
| Global Distribution | 0x00000002 | Mail distribution only |

### OU attributes

| Attribute | Notes |
|-----------|-------|
| `ou` | The OU name (used as RDN) |
| `name` | Same as `ou` |
| `description` | Free text |
| `gPLink` | GPOs linked to this OU |

---

## LDAP filter syntax

### Operators

| Operator | Syntax | Example |
|----------|--------|---------|
| Equality | `(attr=value)` | `(sAMAccountName=jsmith)` |
| Presence | `(attr=*)` | `(mail=*)` |
| Substring | `(attr=*sub*)` | `(cn=John*)` |
| AND | `(&(...)(...))`  | `(&(objectClass=user)(department=IT))` |
| OR | `(\|(...)(...))`  | `(\|(sAMAccountName=a)(sAMAccountName=b))` |
| NOT | `(!(...))`  | `(!(userAccountControl:1.2.840.113556.1.4.803:=2))` |
| Bitwise AND (extensible) | `(attr:1.2.840.113556.1.4.803:=value)` | Used for `userAccountControl` flag checks |
| Bitwise OR (extensible) | `(attr:1.2.840.113556.1.4.803:=value)` | Less common |

### Common filters

```
# All enabled users
(&(objectClass=user)(objectCategory=person)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))

# All disabled users
(&(objectClass=user)(objectCategory=person)(userAccountControl:1.2.840.113556.1.4.803:=2))

# User by sAMAccountName
(&(objectClass=user)(sAMAccountName=jsmith))

# All security groups
(&(objectClass=group)(groupType:1.2.840.113556.1.4.803:=2147483648))

# Members of a specific group (direct only)
(&(objectClass=user)(memberOf=CN=MyGroup,OU=Groups,DC=corp,DC=local))

# Service accounts (by OU)
(&(objectClass=user)(distinguishedName=*OU=ServiceAccounts*))

# All OUs under a base
(objectClass=organizationalUnit)
```

### LDAP injection prevention

**Never build filters by string concatenation.** Always escape user-supplied values.

Characters that must be escaped in filter values:

| Character | Escaped form |
|-----------|-------------|
| `*` | `\2a` |
| `(` | `\28` |
| `)` | `\29` |
| `\` | `\5c` |
| `NUL` | `\00` |

In .NET with `System.DirectoryServices.Protocols`, use `DirectoryAttributeModification` and parameterized `SearchRequest` with escaped values. Never use `string.Format` or interpolation to build filter strings.

---

## ACL / DACL structure in AD

AD security is based on Windows Security Descriptors (defined in MS-DTYP). Key concepts:

- **Security Descriptor**: attached to every AD object. Contains Owner SID, Group SID, DACL, SACL.
- **DACL** (Discretionary ACL): list of ACEs that control access. Order matters — DENY ACEs evaluated before ALLOW.
- **SACL** (System ACL): auditing rules. Requires `SeSecurityPrivilege` to read/write.
- **ACE** (Access Control Entry): `(Type, Flags, Rights, ObjectType, InheritedObjectType, SID)`
  - Type: Allow or Deny
  - Rights: bitmask of `ActiveDirectoryRights` values
  - ObjectType GUID: scopes the ACE to a specific attribute or object class (property-specific ACEs)
  - Inheritance flags: whether ACE propagates to child objects

### ActiveDirectoryRights values (most used)

| Right | Notes |
|-------|-------|
| `GenericAll` | Full control |
| `GenericRead` | Read all properties + list object |
| `ReadProperty` | Read a specific property (use with ObjectType GUID) |
| `WriteProperty` | Write a specific property |
| `CreateChild` | Create child objects (use with ObjectType = object class GUID) |
| `DeleteChild` | Delete child objects |
| `DeleteTree` | Delete object and all children |
| `ExtendedRight` | Extended rights (reset password, etc.) — use with ObjectType = right GUID |
| `WriteDacl` | Modify the DACL |
| `WriteOwner` | Change owner |

### Common extended right GUIDs

| Right | GUID |
|-------|------|
| Reset Password | `00299570-246d-11d0-a768-00aa006e0529` |
| Force Change Password | `00299570-246d-11d0-a768-00aa006e0529` |
| Add/Remove self as member | `bf9679c0-0de6-11d0-a285-00aa003049e2` |
| Send As | `ab721a54-1e2f-11d0-9819-00aa0040529b` |
| Receive As | `ab721a56-1e2f-11d0-9819-00aa0040529b` |

### Reading ACLs in .NET

```csharp
// Using System.DirectoryServices
using var entry = new DirectoryEntry($"LDAP://CN=user,OU=Users,DC=corp,DC=local");
var security = entry.ObjectSecurity;
var dacl = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
```

**Important:** `GetAccessRules(includeExplicit, includeInherited, ...)` — always pass `true` for both unless you intentionally want to exclude inherited ACEs.

---

## Kerberos / LDAP connection notes for .NET

- `LdapConnection` from `System.DirectoryServices.Protocols` is the correct class for raw LDAP.
- `LdapDirectoryIdentifier` takes host and port (default 389 for LDAP, 636 for LDAPS, 3268 for GC, 3269 for GC over SSL).
- For Kerberos auth: use `AuthType.Kerberos` with `NetworkCredential`. On Linux, requires a valid keytab or ticket cache.
- For simple bind (service account): use `AuthType.Basic` over LDAPS only — never over plain LDAP.
- `DirectoryServices.AccountManagement` (`PrincipalContext`) is a higher-level wrapper but loses precision for complex filters and ACL operations. Prefer it for Spec 4/5/6 user/group CRUD, avoid it for Spec 7 ACL work.
- TLS: set `LdapSessionOptions.SecureSocketLayer = true` for LDAPS. For StartTLS, use `StartTransportLayerSecurity()`.
- Connection timeout: set `LdapConnection.Timeout` — default is infinite. Always set explicitly.

---

## LDAP port reference

| Port | Protocol | Use |
| --- | --- | --- |
| 389 | LDAP | Standard — use StartTLS before binding |
| 636 | LDAPS | LDAP over SSL/TLS |
| 3268 | GC LDAP | Global Catalog — cross-domain searches (read only) |
| 3269 | GC LDAPS | Global Catalog over SSL |

---

## AD DS logical structure and DN conventions

The hierarchy from largest to smallest: **Forest → Tree → Domain → Site → OU → Object**.

DN construction rules:

- Each DNS label of the domain name becomes one `DC=` component. `corp.local` → `DC=corp,DC=local`.
- Child domain `child.corp.local` → `DC=child,DC=corp,DC=local`.
- DN reads right-to-left: the rightmost component is the forest root.
- OU path in the DN is bottom-up: `OU=Paris,OU=France,OU=EMEA` means EMEA contains France contains Paris.
- Regular containers (not OUs) use `CN=` — e.g. the built-in `CN=Users,DC=corp,DC=local`.
- Object placement determines the `distinguishedName` — changing OU requires a Move operation (LDAP ModifyDN), not an attribute write.

---

## AD naming contexts (partitions)

Every DC exposes these partitions. Query RootDSE to discover them programmatically.

| Partition | Base DN | Contains |
| --- | --- | --- |
| Domain NC | `DC=corp,DC=local` | Users, groups, computers, OUs — all day-to-day objects |
| Configuration NC | `CN=Configuration,DC=corp,DC=local` | Forest-wide: sites, services, schema links, partitions |
| Schema NC | `CN=Schema,CN=Configuration,DC=corp,DC=local` | Class and attribute definitions |
| Application NC (example) | `DC=DomainDnsZones,DC=corp,DC=local` | Optional, e.g. AD-integrated DNS zones |

---

## RootDSE — discovery attributes

Always query empty string `""` with `SearchScope.Base` to bootstrap connection config:

| Attribute | Value example | Use |
| --- | --- | --- |
| `defaultNamingContext` | `DC=corp,DC=local` | Base DN for domain searches |
| `configurationNamingContext` | `CN=Configuration,DC=corp,DC=local` | Sites, services, partitions |
| `schemaNamingContext` | `CN=Schema,CN=Configuration,DC=corp,DC=local` | Schema queries |
| `rootDomainNamingContext` | `DC=corp,DC=local` | Forest root domain |
| `domainFunctionality` | `7` | Domain Functional Level (0=2000 … 7=2016+) |
| `forestFunctionality` | `7` | Forest Functional Level |
| `highestCommittedUSN` | `12345678` | Current USN — baseline for change tracking |
| `supportedSASLMechanisms` | `GSSAPI GSS-SPNEGO` | Available auth methods |
| `dsServiceName` | DN of the DC | Identifies which DC you're talking to |

---

## Global Catalog

- Holds a **partial attribute set** from every domain in the forest — enough to locate and identify objects.
- Attributes replicated to GC have `isMemberOfPartialAttributeSet = TRUE` in the schema.
- **Read-only via GC port** — writes must go to the object's home domain DC.
- Use GC (3268/3269) when: searching across domains, resolving UPN to domain, or locating a user without knowing their domain.
- Common attributes in GC: `sAMAccountName`, `userPrincipalName`, `mail`, `displayName`, `objectSid`, `objectGUID`, `memberOf`.
- Attributes often NOT in GC: custom/extended attributes, `thumbnailPhoto`, operational attributes.

---

## FSMO roles — relevance for LDAP code

Five roles, two scopes:

**Per-forest (one DC total):**

- **Schema Master**: only DC that accepts schema modifications. Target explicitly for schema extension operations.
- **Domain Naming Master**: manages adding/removing domains. Not relevant for application LDAP.

**Per-domain (one DC per domain):**

- **PDC Emulator**: handles password change replication, account lockout propagation, and is the authoritative time source. **Always target the PDC Emulator for password set/reset operations** and for reading the most current lockout state.
- **RID Master**: allocates RID pools for SID generation. No direct LDAP operation impact.
- **Infrastructure Master**: maintains cross-domain group membership phantoms. Relevant only when group members span multiple domains.

Finding the PDC Emulator: query `fSMORoleOwner` attribute on the domain NC root object, or read `pdcEmulatorName` from a Windows-side API. In code, connect to the target DC and query its RootDSE `dsServiceName`, then compare to the `fSMORoleOwner` on the domain object.

---

## LDAP controls for production searches

Register controls on `SearchRequest.Controls` before sending.

| Control | OID | Purpose |
| --- | --- | --- |
| Paged Results | `1.2.840.113556.1.4.319` | Required for result sets > 1000. Use `PageResultRequestControl`. |
| Sort | `1.2.840.113556.1.4.473` | Server-side sort. Combine with paged results. |
| DirSync | `1.2.840.113556.1.4.841` | Incremental change feed. Requires `DS-Replication-Get-Changes`. |
| Show Deleted | `1.2.840.113556.1.4.417` | Include tombstoned objects in results. |
| Show Recycled | `1.2.840.113556.1.4.2064` | Include recycled objects (AD Recycle Bin enabled). |
| SD Flags | `1.2.840.113556.1.4.801` | Control which SD parts are returned. Flags: 1=Owner, 2=Group, 4=DACL, 8=SACL. |

Paged search pattern:

```csharp
var pageControl = new PageResultRequestControl(pageSize: 1000);
request.Controls.Add(pageControl);
do {
    var response = (SearchResponse)connection.SendRequest(request);
    var pageResponse = (PageResultResponseControl)response.Controls
        .OfType<PageResultResponseControl>().FirstOrDefault();
    // process response.Entries
    pageControl.Cookie = pageResponse?.Cookie ?? Array.Empty<byte>();
} while (pageControl.Cookie.Length > 0);
```

---

## Object lifecycle — deletion, tombstones, Recycle Bin

**Standard deletion (no Recycle Bin):**

- Object is converted to a tombstone: moved to `CN=Deleted Objects,DC=corp,DC=local`, `isDeleted=TRUE`, most attributes stripped.
- Tombstone lifetime: default 180 days. After that, permanently purged.

**AD Recycle Bin** (requires Forest Functional Level 2008 R2 / level 4+):

- Deleted objects retain ALL attributes. `isDeleted=TRUE`, `isRecycled` NOT set.
- Fully recycled objects (past recycle stage): `isDeleted=TRUE` AND `isRecycled=TRUE`.
- Original OU stored in `msDS-LastKnownRDN`.
- Restore: clear `isDeleted`, move back to original OU — requires `Recycle-a-Deleted-Object` extended right (GUID `69ae6200-7f46-11d2-b9ad-00c04f79f805`).

Search for deleted objects:

```csharp
var request = new SearchRequest(
    "CN=Deleted Objects,DC=corp,DC=local",
    "(&(isDeleted=TRUE)(cn=TargetUser*))",
    SearchScope.OneLevel, null);
request.Controls.Add(new ShowDeletedControl());
```

---

## LDAP referrals

When a DC receives a search that spans domain boundaries, it may return LDAP referrals (pointers to other DCs).

- `LdapConnection` follows referrals by default (`ReferralChasingOptions.All`).
- Referral chasing reuses the same credentials — will fail across untrusted domains or when the referred DC is unreachable.
- Disable when search scope is intentionally single-domain:

```csharp
connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
```

- For cross-domain searches, prefer the **Global Catalog (port 3268)** instead of chasing referrals.

---

## Schema queries

To discover class or attribute definitions at runtime:

```text
BaseDN: CN=Schema,CN=Configuration,DC=corp,DC=local
Scope:  OneLevel

Class definition:
  Filter: (&(objectClass=classSchema)(lDAPDisplayName=user))
  Attributes: subClassOf, systemMustContain, systemMayContain, mustContain, mayContain

Attribute definition:
  Filter: (&(objectClass=attributeSchema)(lDAPDisplayName=sAMAccountName))
  Attributes: attributeSyntax, oMSyntax, isSingleValued, rangeLower, rangeUpper, schemaIDGUID
```

`objectClass` hierarchy for common types:

- user → organizationalPerson → person → top
- group → top
- organizationalUnit → top
- computer → user → organizationalPerson → person → top (computers are a subclass of user)

---

## Password policies

**Default Domain Password Policy** — one per domain, applied to all accounts without a PSO.
Read from the domain NC root object (`DC=corp,DC=local`): `pwdHistoryLength`, `maxPwdAge`, `minPwdAge`, `minPwdLength`, `lockoutThreshold`, `lockoutDuration`, `lockoutObservationWindow`.

**Fine-Grained Password Policy (PSO)** — requires Domain Functional Level 2008 (level 3+):

- Stored in: `CN=Password Settings Container,CN=System,DC=corp,DC=local`
- Object class: `msDS-PasswordSettings`
- Applied to users/groups via: `msDS-PSOAppliesTo` (multi-value DN)
- Resultant PSO for a user: read `msDS-ResultantPSO` attribute on the user object (computed by DC, not stored)
- PSO with lowest `msDS-PasswordSettingsPrecedence` value wins when multiple PSOs apply

---

## Tracking changes (USN / DirSync)

**USN-based polling** (simpler, no special rights):

- Every write increments the DC's USN. Each object stores `uSNChanged` and `uSNCreated`.
- Baseline: read `highestCommittedUSN` from RootDSE at startup.
- Poll with filter: `(uSNChanged>=<lastUSN>)` ordered by `uSNChanged` ascending. Update baseline after each batch.
- Limitation: USNs are per-DC. If load-balanced across DCs, track USN per target DC.

**DirSync control** (replication-style, requires `DS-Replication-Get-Changes` extended right):

- Returns only changed objects/attributes since the last cookie.
- Cookie is opaque and DC-specific — store and reuse per target DC.
- Use OID `1.2.840.113556.1.4.841` with `DirectorySynchronizationOptions` in `System.DirectoryServices.Protocols`.

---

## Microsoft Learn reference URLs

| Topic | URL |
| --- | --- |
| AD DS Overview | [AD DS Overview on Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/get-started/virtual-dc/active-directory-domain-services-overview) |
| Win32 AD Programming Guide | [Using AD DS — Win32 apps](https://learn.microsoft.com/en-us/windows/win32/ad/using-active-directory-domain-services) |
| Searching in AD DS (Win32) | [Searching in Active Directory Domain Services](https://learn.microsoft.com/en-us/windows/win32/ad/searching-in-active-directory-domain-services) |
| Creating and Deleting Objects | [Creating and Deleting Objects in AD DS](https://learn.microsoft.com/en-us/windows/win32/ad/creating-and-deleting-objects-in-active-directory-domain-services) |
| Controlling Access to Objects | [Controlling Access to Objects in AD DS](https://learn.microsoft.com/en-us/windows/win32/ad/controlling-access-to-objects-in-active-directory-domain-services) |
| Global Catalog (Win32) | [Global Catalog — Win32 reference](https://learn.microsoft.com/en-us/windows/win32/ad/global-catalog) |
| Application Directory Partitions | [Application Directory Partitions](https://learn.microsoft.com/en-us/windows/win32/ad/application-directory-partitions) |
| Tracking Changes (DirSync) | [Tracking Changes with DirSync](https://learn.microsoft.com/en-us/windows/win32/ad/tracking-changes) |
| System.DirectoryServices.Protocols | [System.DirectoryServices.Protocols API reference](https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.protocols) |
| MS-ADTS (core AD spec) | [MS-ADTS Open Specification](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/d2435927-0999-4c62-8c6d-13ba31a52e1a) |
| .NET API docs source (GitHub) | [dotnet/dotnet-api-docs](https://github.com/dotnet/dotnet-api-docs) |

---

## System.DirectoryServices.Protocols — type reference

This is the raw LDAP layer used for all wire-level operations. Source: `xml/System.DirectoryServices.Protocols/` in dotnet/dotnet-api-docs.

### LdapConnection

Implements `IDisposable`. Inherits `DirectoryConnection`. Always wrap in `using`.

```csharp
// Preferred constructor — explicit credentials + auth type
var conn = new LdapConnection(
    new LdapDirectoryIdentifier("dc.corp.local", 636),
    new NetworkCredential("svc-coreapi", "password", "corp.local"),
    AuthType.Kerberos);
conn.SessionOptions.SecureSocketLayer = true;
conn.SessionOptions.ProtocolVersion = 3;
conn.Timeout = TimeSpan.FromSeconds(30);
conn.Bind(); // explicit bind; without this, LDAP v3 runs as anonymous
```

Key members:

| Member | Notes |
| --- | --- |
| `AuthType` | See AuthType enum below. Set before `Bind()`. |
| `Timeout` | `TimeSpan`. Default is infinite — **always set explicitly**. |
| `SessionOptions` | Returns `LdapSessionOptions`. Set TLS, referrals, signing here. |
| `AutoBind` | When `true`, binds automatically on first request. Prefer explicit `Bind()`. |
| `Bind()` | Sends LDAP bind. Throws `LdapException` on failure. |
| `Bind(NetworkCredential)` | Bind with different credentials than constructor. |
| `SendRequest(DirectoryRequest)` | Synchronous. Returns `DirectoryResponse`. Throws `LdapException`, `DirectoryOperationException`. |
| `SendRequest(DirectoryRequest, TimeSpan)` | With per-request timeout override. |
| `BeginSendRequest(...)` / `EndSendRequest(...)` | Async pair. Use for long-running searches. |

### AuthType enum

| Value | Int | When to use |
| --- | --- | --- |
| `Anonymous` | 0 | Never in production. |
| `Basic` | 1 | Simple bind with username/password. **LDAPS only** — plaintext over LDAP is forbidden. |
| `Negotiate` | 2 | Windows auto-selects Kerberos or NTLM. Default when no authType specified. |
| `Ntlm` | 3 | Force NTLM. Avoid — Kerberos is preferred. |
| `Kerberos` | 9 | Explicit Kerberos. Use for service account binds in domain-joined environments. |
| `External` | 8 | Client certificate authentication (TLS mutual auth). |

### LdapSessionOptions (key properties)

| Property | Type | Notes |
| --- | --- | --- |
| `SecureSocketLayer` | bool | `true` = LDAPS. Set before `Bind()`. |
| `ProtocolVersion` | int | Always set to `3`. |
| `ReferralChasing` | `ReferralChasingOptions` | Default `All`. Set to `None` for single-domain scope. |
| `Signing` | bool | Kerberos message integrity. Set alongside `Sealing`. |
| `Sealing` | bool | Kerberos message encryption. Pairs with `Signing`. |
| `VerifyServerCertificate` | delegate | Callback to validate the DC's TLS certificate. |
| `SendTimeout` | TimeSpan | Per-send operation timeout. Throws on negative. |
| `PingKeepAliveTimeout` | TimeSpan | Interval before keep-alive ping is sent. |

### SearchRequest / SearchResponse

```csharp
var request = new SearchRequest(
    distinguishedName: "OU=Users,DC=corp,DC=local",
    ldapFilter: "(&(objectClass=user)(sAMAccountName=jsmith))",
    searchScope: SearchScope.Subtree,
    attributeList: new[] { "cn", "mail", "userAccountControl" });

request.SizeLimit = 0;              // 0 = server default
request.TimeLimit = TimeSpan.FromSeconds(30);
request.TypesOnly = false;          // false = return names AND values

var response = (SearchResponse)connection.SendRequest(request);
foreach (SearchResultEntry entry in response.Entries)
{
    string dn = entry.DistinguishedName;
    string cn = entry.Attributes["cn"]?[0] as string;
}
```

`Attributes` property: pass `null` to retrieve all attributes. Pass `new[] { "1.1" }` to retrieve DN only (no attributes, fastest).

`Filter` accepts either an LDAP filter string or a DSML v2 XML document — anything else throws `ArgumentException`.

### AddRequest — creating objects

```csharp
var req = new AddRequest("CN=NewUser,OU=Users,DC=corp,DC=local",
    new DirectoryAttribute("objectClass", "user"),
    new DirectoryAttribute("sAMAccountName", "newuser"),
    new DirectoryAttribute("userAccountControl", "512"),   // enabled normal account
    new DirectoryAttribute("unicodePwd", EncodePassword("P@ssw0rd")));

connection.SendRequest(req);
```

`Attributes` collection is read-only after construction — populate via the params array in the constructor or `req.Attributes.Add(new DirectoryAttribute(...))`.

### ModifyRequest — updating attributes

Three operations via `DirectoryAttributeOperation` enum:

| Value | Int | Meaning |
| --- | --- | --- |
| `Add` | 0 | Add value(s) to a multi-valued attribute. Error if attribute doesn't exist. |
| `Delete` | 1 | Remove specific value(s). Pass no values to remove the entire attribute. |
| `Replace` | 2 | Replace all current values with the supplied values. Pass no values to clear the attribute. |

```csharp
var mod = new DirectoryAttributeModification
{
    Name = "mail",
    Operation = DirectoryAttributeOperation.Replace
};
mod.Add("newmail@corp.local");

var req = new ModifyRequest("CN=User,OU=Users,DC=corp,DC=local", mod);
connection.SendRequest(req);
```

Alternative single-operation constructor:

```csharp
var req = new ModifyRequest(
    "CN=User,OU=Users,DC=corp,DC=local",
    DirectoryAttributeOperation.Replace,
    "mail",
    new object[] { "newmail@corp.local" });
```

### DeleteRequest

```csharp
connection.SendRequest(new DeleteRequest("CN=User,OU=Users,DC=corp,DC=local"));
```

Deletes a leaf object. To delete a container with children, use a DeleteTree extended operation or delete children first.

### ModifyDNRequest — rename or move

```csharp
// Rename only (same OU)
var rename = new ModifyDNRequest(
    distinguishedName: "CN=OldName,OU=Users,DC=corp,DC=local",
    newParentDistinguishedName: null,           // null = same parent
    newName: "CN=NewName")
{ DeleteOldRdn = true };                        // always true unless preserving old CN value

// Move to different OU (can also rename in one operation)
var move = new ModifyDNRequest(
    distinguishedName: "CN=User,OU=OldOU,DC=corp,DC=local",
    newParentDistinguishedName: "OU=NewOU,DC=corp,DC=local",
    newName: "CN=User")
{ DeleteOldRdn = true };

connection.SendRequest(move);
```

`DeleteOldRdn = true` is the correct value for all normal rename/move operations. Set to `false` only if the schema allows multi-valued RDN (rare).

---

## System.DirectoryServices.AccountManagement — type reference

Higher-level wrapper over ADSI. Prefer for Specs 4/5/6 (user/group CRUD). Avoid for Spec 7 (ACL) and complex filter operations. Source: `xml/System.DirectoryServices.AccountManagement/` in dotnet/dotnet-api-docs.

### PrincipalContext

The connection context. Must be `Dispose()`d. Always use `ContextType.Domain` for AD DS.

```csharp
using var ctx = new PrincipalContext(
    ContextType.Domain,
    "corp.local",                                   // domain name or DC FQDN
    "OU=Users,DC=corp,DC=local",                    // default container for new objects
    ContextOptions.Negotiate | ContextOptions.Signing | ContextOptions.Sealing,
    "svc-coreapi@corp.local",                       // service account UPN
    "password");
```

`container` parameter: the OU where `Save()` places new objects by default. Pass `null` for domain root.

`ConnectedServer` property: the actual DC the context is bound to (useful for logging).

`ValidateCredentials(userName, password)` — returns `bool`. Username: bare name only (not `DOMAIN\user`).

### UserPrincipal

Maps to AD user objects. Key members:

| Property | AD Attribute | Notes |
| --- | --- | --- |
| `SamAccountName` | `sAMAccountName` | Required. Max 20 chars. |
| `UserPrincipalName` | `userPrincipalName` | UPN format `user@domain`. |
| `DisplayName` | `displayName` | |
| `GivenName` | `givenName` | First name. |
| `Surname` | `sn` | Last name. |
| `EmailAddress` | `mail` | |
| `VoiceTelephoneNumber` | `telephoneNumber` | |
| `EmployeeId` | `employeeID` | |
| `Enabled` | `userAccountControl` bit | `true`/`false`/`null`. |
| `PasswordNeverExpires` | `userAccountControl` bit | |
| `PasswordNotRequired` | `userAccountControl` bit | |
| `AccountExpirationDate` | `accountExpires` | `DateTime?` |
| `LastPasswordSet` | `pwdLastSet` | Read-only. |

Lifecycle:

```csharp
// Create
using var user = new UserPrincipal(ctx, "jsmith", "P@ssw0rd!", enabled: true);
user.DisplayName = "John Smith";
user.Save();   // writes to AD; must call before using the object further

// Find
using var found = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, "jsmith");

// Update
found.DisplayName = "John A. Smith";
found.Save();

// Password
found.SetPassword("NewP@ss!");
found.ExpirePasswordNow();   // force change on next logon

// Delete
found.Delete();
```

`IdentityType` values: `SamAccountName`, `DistinguishedName`, `Sid`, `Guid`, `UserPrincipalName`, `Name`.

### GroupPrincipal

Maps to AD group objects. Key members:

| Property | Notes |
| --- | --- |
| `SamAccountName` | Group name. |
| `IsSecurityGroup` | `Nullable<bool>`. `true` = security group. `null` before first `Save()`. |
| `GroupScope` | `GroupScope` enum: `Local`, `Global`, `Universal`. |
| `Members` | `PrincipalCollection` — read/write. Call `Save()` to commit changes. |

Lifecycle and membership:

```csharp
// Create
using var grp = new GroupPrincipal(ctx) { Name = "AppAdmins", IsSecurityGroup = true };
grp.Save();

// Add member
using var user = UserPrincipal.FindByIdentity(ctx, "jsmith");
grp.Members.Add(user);
grp.Save();

// Remove member
grp.Members.Remove(user);
grp.Save();

// Recursive membership check
bool isMember = grp.GetMembers(recursive: true).Contains(user);

// Find
using var found = GroupPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, "AppAdmins");
```

Important limitation: members linked via `primaryGroupID` (e.g. default "Domain Users") cannot be removed via the Members collection.

---

## API design reference (KopiCloud-AD-API patterns)

Source: [github.com/KopiCloud-AD-API](https://github.com/KopiCloud-AD-API) — a real-world production AD REST API. Use these patterns as a proven reference for controller design and DTO field lists. Their implementation runs on Windows (IIS + domain-joined), but the API surface design and AD field mappings apply directly.

### REST endpoint conventions

| Resource | List | Get | Create | Update | Delete | Special |
| --- | --- | --- | --- | --- | --- | --- |
| User | `GET /v1/users` | `GET /v1/users/{username}` | `POST /v1/users/{username}` | `PUT /v1/users/{username}` | `DELETE /v1/users/{username}` | Enable/Disable/Unlock/ResetPassword/Rename |
| Group | `GET /v1/groups` | `GET /v1/groups/{name}` | `POST /v1/groups/{name}/security` | — | `DELETE /v1/groups/{name}` | Rename |
| Membership | — | `GET /v1/users/{u}/groups/{g}` | `POST /v1/users/{u}/groups/{g}` | — | `DELETE /v1/users/{u}/groups/{g}` | List all groups for user |
| OU | `GET /v1/ous` | `GET /v1/ous` (by path) | `POST /v1/ous` | `PUT /v1/ous` | `DELETE /v1/ous/{path}` | Move, Rename |

URL design notes:

- Separate endpoints for `Enable`, `Disable`, `Unlock`, `ResetPassword`, `Rename` — don't fold them into generic PUT (makes intent explicit and auditable)
- List endpoints accept `OUPath` and `Recursive` query params for scoped searches
- Both username and GUID should be supported as identifiers (GUID is stable across renames)

### Response envelope

```json
{
  "output": "Operation completed successfully.",
  "result": { /* single object or array */ }
}
```

For errors, use RFC 7807 Problem Details (as required by coreapi evaluation criteria), not this envelope.

### User DTO — complete field reference

Derived from KopiCloud's 45-field user object. The AD attribute mapping is shown where it differs from the camelCase field name.

**Identity:**

| DTO field | AD attribute | Notes |
| --- | --- | --- |
| `guid` | `objectGUID` | Binary → format as UUID string. Stable across renames. |
| `username` / `samAccountName` | `sAMAccountName` | Max 20 chars, unique per domain. |
| `userPrincipalName` | `userPrincipalName` | UPN format. |
| `displayName` | `displayName` | |

**Name components:**

| DTO field | AD attribute |
| --- | --- |
| `firstName` | `givenName` |
| `lastName` | `sn` |
| `initials` | `initials` |

**Contact:**

| DTO field | AD attribute |
| --- | --- |
| `emailAddress` | `mail` |
| `officePhone` | `telephoneNumber` |
| `homePhone` | `homePhone` |
| `mobilePhone` | `mobile` |

**Organization:**

| DTO field | AD attribute |
| --- | --- |
| `jobTitle` | `title` |
| `department` | `department` |
| `company` | `company` |
| `office` | `physicalDeliveryOfficeName` |
| `manager` | `manager` (DN value) |
| `description` | `description` |
| `ouPath` | parent path from `distinguishedName` |

**Address:**

| DTO field | AD attribute |
| --- | --- |
| `streetAddress` | `streetAddress` |
| `streetPoBox` | `postOfficeBox` |
| `city` | `l` (lowercase L) |
| `state` | `st` |
| `postalCode` | `postalCode` |
| `country` | `c` (ISO 3166-1 alpha-2) / `co` (display name) / `countryCode` (numeric) |

**Security flags** (all `userAccountControl` bits):

| DTO field | AD attribute / bit |
| --- | --- |
| `enabled` | `userAccountControl` bit `ACCOUNTDISABLE` (0x0002) inverted |
| `passwordNeverExpired` | `userAccountControl` bit `DONT_EXPIRE_PASSWD` (0x10000) |
| `passwordNotRequired` | `userAccountControl` bit `PASSWD_NOTREQD` (0x0020) |
| `changePasswordNextLogon` | `pwdLastSet = 0` (set to 0 to force; -1 to clear) |

**Profile paths:**

| DTO field | AD attribute |
| --- | --- |
| `profilePath` | `profilePath` |
| `profileLogonScript` | `scriptPath` |
| `homeFolderPath` | `homeDirectory` |
| `homeFolderDrive` | `homeDrive` (e.g. `H:`) |
| `homeFolderDirectory` | same as `homeDirectory` |

**Remote Desktop Services (RDS/Terminal Services) attributes:**
These are NOT standard LDAP attributes — they are packed into the binary `userParameters` blob. They can only be read/written reliably via ADSI (`DirectoryEntry`), NOT via `LdapConnection` raw attribute reads. Use `DirectoryEntry.InvokeSet()`/`InvokeGet()` with the property name, or set via the `IADsTSUserEx` COM interface.

| DTO field | ADSI property name |
| --- | --- |
| `rdsProfilePath` | `TerminalServicesProfilePath` |
| `rdsHomeFolderPath` | `TerminalServicesHomeDirectory` |
| `rdsHomeFolderDrive` | `TerminalServicesHomeDrive` |
| `rdsAllowLogon` | `AllowLogon` (1=allow, 0=deny) |
| `rdsConnectDrive` | `ConnectClientDrives` |

For Specs 4/5 (User/Service account CRUD), skip RDS fields unless explicitly required — they need ADSI COM interop and add significant complexity.

### Group DTO

| DTO field | AD attribute / value |
| --- | --- |
| `guid` | `objectGUID` |
| `name` | `cn` / `sAMAccountName` |
| `description` | `description` |
| `email` | `mail` |
| `ouPath` | parent path from `distinguishedName` |
| `type` | Derived from `groupType` bit: `Security` or `Distribution` |
| `scope` | Derived from `groupType` bits: `Global`, `DomainLocal`, `Universal` |

`type` and `scope` are both packed into the single `groupType` integer — decode separately.

### OU DTO

| DTO field | AD attribute | Notes |
| --- | --- | --- |
| `guid` | `objectGUID` | |
| `name` | `ou` | The OU name (RDN) |
| `description` | `description` | |
| `path` | `distinguishedName` | Full DN |
| `protected` | DACL DENY ACE | See below |

**OU `protected` flag** — how "Protect from accidental deletion" works in AD:
This is NOT a stored attribute. It is implemented as a DENY ACE on the object's DACL:

- DENY `Delete` and `DeleteTree` rights for `Everyone` (SID `S-1-1-0`) on the object itself
- AND a DENY `DeleteChild` ACE for `Everyone` on the **parent** object scoped to this object's class

To read: inspect the DACL for a DENY ACE with `Everyone` and `Delete`/`DeleteTree` rights.
To set (protect): add those DENY ACEs.
To unset (unprotect): remove those DENY ACEs before deleting the OU.
This is a Spec 7 (ACL) concern — OU delete in Spec 6 must call the unprotect logic first when `force=true`.

### Service account minimum AD permissions

KopiCloud runs as Domain Admin, which is too broad. For coreapi, scope to minimum:

| Operation | Required AD permission |
| --- | --- |
| Read users/groups/OUs | `ReadProperty` on target OU subtree |
| Create users | `CreateChild` (user class) on target OU |
| Modify user attributes | `WriteProperty` on user objects in target OU |
| Delete users | `DeleteChild` on parent OU + `Delete` on user objects |
| Reset password | `ExtendedRight` — Reset Password GUID on user objects |
| Enable/Disable accounts | `WriteProperty` on `userAccountControl` |
| Create/delete groups | `CreateChild`/`DeleteChild` (group class) on target OU |
| Modify group membership | `WriteProperty` on `member` attribute of group objects |
| Create/delete OUs | `CreateChild`/`DeleteChild` (organizationalUnit class) on parent OU |
| Rename/move objects | `WriteProperty` on `cn`/`ou` + `DeleteChild` (source) + `CreateChild` (destination) |
| Read/write DACLs | `ReadControl` + `WriteDacl` on target objects |

Document these permissions explicitly and grant them at the OU level rather than domain-wide — required by Spec 9 evaluation criteria.
