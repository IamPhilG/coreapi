# CoreApi.IntegrationTests

These tests exercise the LDAP connection layer and `UserService` **against a real Active Directory
Domain Controller**. They are deliberately **excluded from the standard pull-request CI**
(`.github/workflows/ci.yml`), which runs only `CoreApi.UnitTests` (unit + HTTP end-to-end guardrail
tests that need neither a DC nor AWS).

Run them only in an environment you are authorized to use. Nothing here provisions a DC or contacts
AWS unless you explicitly opt in.

## Authorized AD target — scope

- **AWS Managed Microsoft AD is not a supported or planned target.** No test or POC may provision
  or use AWS Managed Microsoft AD.
- Real AD tests must run **only against a self-managed AD DS** (a domain controller you operate
  yourself) in an **explicitly authorized** environment.
- The **standard CI connects to no real AD** of any kind: it runs only `CoreApi.UnitTests` (see
  `.github/workflows/ci.yml`).

## Test categories

Every test in this project is tagged `[Trait("Category", "Integration")]`. To run only the
DC-backed tests, or to exclude them elsewhere:

```powershell
# Run the AD integration tests (requires a reachable DC — see below)
dotnet test tests/CoreApi.IntegrationTests/CoreApi.IntegrationTests.csproj

# Exclude them from any wider run
dotnet test --filter "Category!=Integration"
```

## Secrets and parameters — never committed

No credential or secret is stored in the repository:

- `tests/**/appsettings.Development.json` is gitignored (it may hold AWS/AD values).
- `appsettings.Development.template.json` is the committed template and contains **empty** values.
- `AdAdminPassword` defaults to empty; a non-empty value must be supplied at runtime only.
- Nothing logs secret values.

### Environment variables

Point the tests at an existing DC without any AWS provisioning by setting:

| Variable | Meaning |
| --- | --- |
| `LDAP__Host` | DC hostname or IP |
| `LDAP__BaseDn` | Base DN, e.g. `DC=corp,DC=local` |
| `LDAP__Port` | LDAP port (389, or 636 for LDAPS) |
| `LDAP__UseTls` | `true` for LDAPS |
| `LDAP__ServiceAccountUser` | Bind account (UPN) |
| `LDAP__ServiceAccountPassword` | Bind account password |

When these are absent, the fixture prints a clear warning and the tests fail fast rather than
connecting anywhere by accident. `TestInfrastructure:ProvisionAdDc` defaults to `false`, so **no
AWS call is ever made unless it is explicitly enabled** in the gitignored
`appsettings.Development.json`.

### Optional auto-provisioning (opt-in, local only)

Setting `TestInfrastructure:ProvisionAdDc=true` in the gitignored `appsettings.Development.json`
launches an EC2 Windows Server DC via the AWS CLI. This is a local developer convenience only; it
requires an AWS credential chain and the settings listed in `appsettings.Development.template.json`,
and it is **never** exercised by CI. A managed secret store (AWS Secrets Manager, environment
injection, etc.) is the documented production path — it is intentionally not created by this repo.
