# Codex Review Findings: Specs 1 and 2

Review date: 2026-06-15

Scope reviewed:
- Spec 1: Project scaffold
- Spec 2: AD DS connection layer

Repository state at review time:
- All repository files were untracked.
- Review treated the entire working tree as proposed implementation.

## Summary

Spec 1 is broadly acceptable as an initial scaffold. The solution, source project, test projects, basic folder layout, `.gitignore`, appsettings skeleton, package lock files, nullable reference types, warnings-as-errors, and `/v1/` base controller route are present.

Spec 2 is partially implemented but should not be marked done yet. The LDAP abstraction and `System.DirectoryServices.Protocols` implementation exist, and unit/build checks pass, but the required real LDAP integration gate failed in this environment. There is also a production TLS enforcement gap that should be fixed before sign-off.

## Verification Performed

### Build

Command:

```powershell
dotnet build -warnaserror
```

Result:
- Passed.
- 0 warnings.
- 0 errors.

### Unit Tests

Command:

```powershell
dotnet test --filter "Category=Unit" --no-build
```

Result:
- Passed.
- 10 unit tests passed.

### Integration Tests

Command:

```powershell
dotnet test --filter "Category=Integration" --no-build
```

Result:
- Failed.
- 2 meaningful LDAP integration tests failed with `System.DirectoryServices.Protocols.LdapException: The LDAP server is unavailable.`
- The tests defaulted to `localhost:389`, and no LDAP server was reachable there.
- 1 placeholder integration test passed, but it does not verify behavior.

### Secret Scan

Command:

```powershell
rg -n "password|secret|token|key|credential|BEGIN|PRIVATE" -S .
```

Result:
- No obvious committed secrets were found.
- Matches were limited to documentation, config placeholder keys, and code references to credentials.

## Findings

### Blocking: Spec 2 Definition of Done is not met because real LDAP integration tests fail

Spec 2 requires integration tests against a real LDAP target. The LDAP integration tests currently fail because the default target is unavailable.

Relevant file:
- `tests/CoreApi.IntegrationTests/Infrastructure/DirectoryConnectionTests.cs`

Relevant lines:
- The tests default to `LDAP__Host` or `localhost`.
- The tests default to `LDAP__Port` or `389`.
- The failing calls are `SearchAsync_RootDse_ReturnsDefaultNamingContext` and `SearchAsync_DomainRoot_ReturnsAtLeastOneEntry`.

Observed error:

```text
System.DirectoryServices.Protocols.LdapException : The LDAP server is unavailable.
```

Impact:
- Codex cannot independently confirm that the Spec 2 LDAP layer works against AD DS or Samba AD DC.
- Under the repository's own Definition of Done, Spec 2 cannot be marked complete until these pass against a real target.

Recommendation:
- Configure a real LDAP target using environment variables or development settings:
  - `LDAP__Host`
  - `LDAP__BaseDn`
  - `LDAP__Port`
  - `LDAP__UseTls`
  - `LDAP__ServiceAccountUser`
  - `LDAP__ServiceAccountPassword`
- Re-run the integration test category.
- Capture the passing result before marking Spec 2 done.

### High: LDAPS is not enforced outside Development

`DirectoryConnectionOptions.UseTls` defaults to `true`, but configuration can override it to `false` in any environment. Startup validation only uses data annotation validation, so a non-development deployment can accidentally run LDAP without TLS.

Relevant files:
- `src/CoreApi/Infrastructure/DirectoryConnectionOptions.cs`
- `src/CoreApi/Program.cs`

Current behavior:
- `UseTls` defaults to `true`.
- `appsettings.Development.json` sets `UseTls` to `false`, which is acceptable for local development.
- No environment-aware validation rejects `UseTls=false` in non-development environments.

Impact:
- This violates the stated security criterion: LDAPS must be enforced in all non-development configurations.
- A deployment misconfiguration could send LDAP traffic, including credentials depending on bind mode, without LDAPS.

Recommendation:
- Add startup validation that rejects `DirectoryConnection:UseTls=false` unless `IHostEnvironment.IsDevelopment()` is true.
- Keep the current development override if needed for local Samba or lab testing.

Suggested shape:

```csharp
builder.Services.AddOptions<DirectoryConnectionOptions>()
    .BindConfiguration(DirectoryConnectionOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(options => builder.Environment.IsDevelopment() || options.UseTls,
        "DirectoryConnection:UseTls must be true outside Development.")
    .ValidateOnStart();
```

### Medium: Placeholder tests create false confidence

There are empty tests in both test projects:

- `tests/CoreApi.UnitTests/UnitTest1.cs`
- `tests/CoreApi.IntegrationTests/UnitTest1.cs`

Impact:
- The integration test run reported one passed test even though all meaningful LDAP tests failed.
- This can make test summaries look healthier than they are.

Recommendation:
- Delete the placeholder `UnitTest1` classes.
- Keep only tests that assert real behavior.

### Medium: LDAP injection safety is delegated to callers

`IDirectoryConnection.SearchAsync` accepts a raw LDAP filter string. The XML comment tells callers to pre-escape user input, but the abstraction does not provide a safe way to construct filters.

Relevant file:
- `src/CoreApi/Infrastructure/IDirectoryConnection.cs`

Impact:
- Later CRUD specs will likely accept caller-controlled values such as user names, group names, UPNs, DNs, or search terms.
- Relying on every caller to remember LDAP escaping is brittle.
- The repository security criteria require LDAP injection prevention.

Recommendation:
- Add a small LDAP filter escaping helper before implementing Specs 4-7.
- Prefer centralizing filter construction in infrastructure or service-layer helpers so later code does not concatenate raw user input into LDAP filters.
- Add unit tests covering LDAP special characters:
  - `*`
  - `(`
  - `)`
  - `\`
  - NUL

## Spec 1 Assessment

Spec 1 requested:
- Empty ASP.NET Core Web API project.
- Solution structure.
- Folder conventions.
- `.gitignore`.
- `appsettings` skeleton with connection strings and JWT config placeholders.

Observed:
- `coreapi.sln` exists.
- `src/CoreApi/CoreApi.csproj` targets `net9.0`.
- `tests/CoreApi.UnitTests` and `tests/CoreApi.IntegrationTests` exist.
- `.gitignore` exists and is a broad Visual Studio/.NET ignore file.
- `appsettings.json` contains `DirectoryConnection` and `Jwt` placeholder sections.
- Controllers and Infrastructure folders exist.
- A base controller route uses `v1/[controller]`.
- Nullable and warnings-as-errors are enabled.
- Package lock files exist.

Conclusion:
- Spec 1 is acceptable, with the minor cleanup recommendation to remove placeholder tests.

## Spec 2 Assessment

Spec 2 requested:
- `IDirectoryConnection` abstraction.
- Implementation using `System.DirectoryServices.Protocols`.
- Config: host, port, base DN, service account credentials, TLS toggle.
- No business logic in this layer.

Observed:
- `IDirectoryConnection` exists.
- `LdapDirectoryConnection` uses `LdapConnection`, `SearchRequest`, `AddRequest`, `ModifyRequest`, `DeleteRequest`, and `ModifyDNRequest`.
- Options include host, port, base DN, service account user/password, TLS toggle, and timeout.
- The implementation has no apparent business-object logic.
- Searches use paged results.
- Requests have timeouts.
- Referral chasing is disabled.

Concerns:
- Real LDAP integration verification failed.
- LDAPS is not enforced outside Development.
- LDAP filter safety is not centralized.
- The `BaseDn` option is configured but not used as a default in `LdapDirectoryConnection.SearchAsync`; callers must always provide a base DN. This is not immediately wrong, but later service code should avoid duplicating base DN handling.

Conclusion:
- Spec 2 should remain open until the blocking integration gate passes and the TLS enforcement gap is fixed.

## Final Recommendation

Codex does not approve Spec 2 as done yet.

Required before sign-off:
- Configure and run integration tests against a real LDAP target.
- Fix non-development TLS enforcement.
- Remove placeholder tests or exclude them from meaningful reporting.

Recommended before Specs 4-7:
- Add a central LDAP filter escaping or construction helper with unit coverage.
