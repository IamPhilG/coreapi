# /run-integration-tests

Runs integration tests against the real LDAP target. Integration tests hit a live AD DS instance — never a mock.

## Usage

```
/run-integration-tests [--filter <test-name-pattern>]
```

- `--filter` (optional): additional xUnit filter expression to scope to a single test class or method

## Instructions

### Step 1 — Verify LDAP target is configured

Two modes. Check which is active:

**Mode A — Manual target (default)**
Read `src/CoreApi/appsettings.Development.json` or check env vars:
- `LDAP__Host` — must be non-empty
- `LDAP__BaseDn` — must be non-empty
- `LDAP__ServiceAccountUser` — must be non-empty

If all three are missing or empty AND Mode B is not active: stop and tell the user to configure a target before running integration tests. Do not proceed.

**Mode B — Auto-provisioned EC2**
Read `tests/CoreApi.IntegrationTests/appsettings.Development.json`.
If `TestInfrastructure:ProvisionAdDc` is `true`, the `AdDcProvisionerFixture` will handle provisioning automatically. No manual env vars needed — proceed directly to build.

Minimum required when ProvisionAdDc is true:
- `TestInfrastructure:AmiId` — OR `TestInfrastructure:ExistingInstanceId` (to reuse a stopped instance)
- `TestInfrastructure:AdAdminPassword`
- AWS credentials available in the environment (env vars, `~/.aws/credentials`, or IAM role)

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
