# /run-integration-tests

Runs integration tests against the real LDAP target. Integration tests hit a live AD DS instance — never a mock.

## Usage

```
/run-integration-tests [--filter <test-name-pattern>]
```

- `--filter` (optional): additional xUnit filter expression to scope to a single test class or method

## Instructions

### Step 1 — Verify LDAP target is configured

Three modes. Check which is active:

**Mode A — Manual target**
Check env vars `LDAP__Host`, `LDAP__BaseDn`, `LDAP__ServiceAccountUser`.
If all three are set and non-empty: proceed to Step 2.

**Mode B — Auto-provisioned EC2**
Read `tests/CoreApi.IntegrationTests/appsettings.Development.json`.
If `TestInfrastructure:ProvisionAdDc` is `true` and the file has `AmiId` (or `ExistingInstanceId`) + `AdAdminPassword` + `SecurityGroupId`: proceed to Step 2. The `AdDcProvisionerFixture` handles the rest.

**First run / not configured**
If neither Mode A nor Mode B is satisfied: stop. Tell the user:

```
No LDAP target configured.

Quick start (recommended):
  .\tools\setup-test-dc.ps1          # test mode
  .\tools\setup-test-dc.ps1 -Mode demo  # demo mode

Manual alternative: set LDAP__Host, LDAP__BaseDn, LDAP__ServiceAccountUser,
LDAP__ServiceAccountPassword environment variables pointing at an existing DC.
```

Do not proceed until one of the two modes is satisfied.

### Step 2 — Build

Run:
```
dotnet build -warnaserror
```

Stop on failure.

### Step 3 — Run integration tests

If `--filter` was provided:
```
dotnet test --filter "Category=Integration&<user-filter>" -v normal
```

Otherwise:
```
dotnet test --filter "Category=Integration" -v normal
```

Stream output. Do not suppress failures.

If Mode B is active, the test run may take 10–15 minutes on first boot (DC promotion) or 2–3 minutes on subsequent runs (instance restart). This is expected — do not timeout early.

### Step 4 — Report

- All pass: print `✓ Integration tests passed against real LDAP target.`
- Any failure: print the failing test name, error message, and stack trace. Do not summarize or hide failures.
- If tests were skipped due to missing LDAP connectivity (common skip reason): flag explicitly — a skipped integration test is not a passing test.
- If Mode B was used and a new instance was launched: remind the user to copy the `ExistingInstanceId` from the test output into `appsettings.Development.json` for faster subsequent runs.
