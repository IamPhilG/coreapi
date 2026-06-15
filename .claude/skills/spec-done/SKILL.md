# /spec-done

Runs the Definition of Done checklist for the spec just implemented. Reports PASS or FAIL with specific failures listed. Never self-certify — run every check mechanically.

## Instructions

### Step 1 — Build

Run:
```
dotnet build -warnaserror
```

- Exit 0 = PASS
- Any warning treated as error = FAIL. Show the warning text.

### Step 2 — Unit tests

Run:
```
dotnet test --filter "Category=Unit" -v normal
```

- All pass = PASS
- Any failure = FAIL. Show the failing test name and message.

### Step 3 — Secret scan on staged files

Run:
```
git diff --cached --name-only
```

For each staged file, check whether the filename matches any of:
`appsettings*.json`, `*.env`, `*secret*`, `*credential*`, `*password*`, `*token*`

- No matches = PASS
- Any match = FAIL. List the file names. Do not proceed until user confirms the file contains no secrets.

### Step 4 — OpenAPI regen reminder

If any file under `Controllers/` was modified in this spec:
- Remind the user to regenerate the OpenAPI spec (`dotnet build` triggers Swashbuckle generation if wired) and commit the updated spec file.
- Do not auto-mark this PASS — ask the user to confirm.

### Step 5 — Integration tests reminder

Remind the user to run:
```
dotnet test --filter "Category=Integration" -v normal
```
against a real LDAP target (not a mock). This cannot be automated here — user must confirm manually.

### Step 6 — Complex build gate check

Check whether the spec just completed meets ANY of these triggers:
- Touches `LdapConnection` or `SearchRequest`
- Uses `ActiveDirectorySecurity`
- Configures Kerberos / GSSAPI
- Modifies `Dockerfile` or container entrypoints
- Wires cross-cutting logic across multiple AD object types

If yes: remind the user that a Codex review is required before marking the spec done.

### Step 7 — Final report

Output a table:

| Check | Result |
|-------|--------|
| Build (no warnings) | PASS / FAIL |
| Unit tests | PASS / FAIL |
| Secret scan | PASS / FAIL |
| OpenAPI regen | Confirmed / Pending |
| Integration tests | Confirmed / Pending |
| Codex review (if complex) | Required / N/A |

If all checks are PASS or Confirmed: print `✓ Spec is DONE`.
If any are FAIL or Pending: print `✗ Spec is NOT done — resolve the items above first`.
