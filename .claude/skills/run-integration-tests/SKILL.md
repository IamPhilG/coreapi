# /run-integration-tests

Runs integration tests against the real LDAP target. Integration tests hit a live AD DS instance — never a mock.

## Usage

```
/run-integration-tests [--filter <test-name-pattern>]
```

- `--filter` (optional): additional xUnit filter expression to scope to a single test class or method

## Instructions

### Step 1 — Verify LDAP target is configured

Read `src/CoreApi/appsettings.Development.json` (or `appsettings.json`).

Check that the following keys are present and non-empty:
- `DirectoryConnection:Host`
- `DirectoryConnection:BaseDn`
- `DirectoryConnection:ServiceAccountUser` (or equivalent credential config)

If any key is missing or empty: stop and tell the user to configure the LDAP target before running integration tests. Do not proceed.

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

### Step 4 — Report

- All pass: print `✓ Integration tests passed against real LDAP target.`
- Any failure: print the failing test name, error message, and stack trace. Do not summarize or hide failures.
- If tests were skipped due to missing LDAP connectivity (common skip reason): flag explicitly — a skipped integration test is not a passing test.
