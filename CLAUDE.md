# coreapi

## Session bootstrap

Read these files at the start of every session before doing any work:

- [.claude/memory/user_profile.md](.claude/memory/user_profile.md)
- [.claude/knowledge-base/project_coreapi_goal.md](.claude/knowledge-base/project_coreapi_goal.md)
- [.claude/memory/feedback_no_writes_outside_repo.md](.claude/memory/feedback_no_writes_outside_repo.md)
- [.claude/knowledge-base/ad-ds-reference.md](.claude/knowledge-base/ad-ds-reference.md)

---

Core AD DS gateway for the Ouritres org. Higher-level topic APIs (user management, group management, etc.) delegate all Active Directory operations to this single service.

## Purpose

- Talks directly to AD DS via LDAP / Kerberos / LDAPS
- Authenticates and authorizes its own callers (the topic APIs)
- Exposes a REST API surface to those callers
- Owns business logic (e.g. default group memberships on user creation)

## Confirmed architectural decisions

| Decision | Choice |
| --- | --- |
| Language / framework | C# / .NET 9 / ASP.NET Core Web API |
| Caller authentication | JWT Bearer token — issuer/authority is **configurable at deploy time**, not hardcoded |
| Deployment | AWS (exact target TBD) |

## Key libraries

- `System.DirectoryServices.Protocols` — raw LDAP
- `System.DirectoryServices.AccountManagement` — users, service accounts, groups
- `System.Security.AccessControl` — ACL management
- ASP.NET Core JWT Bearer middleware — caller auth
- `Swashbuckle.AspNetCore` — OpenAPI spec generation + Swagger UI

---

## Environment

| Tool | Version |
| --- | --- |
| .NET SDK | 9.0.305 |
| Docker | 29.1.3 |
| AWS CLI | 2.32.19 |
| PowerShell | 5.1 |
| Git | 2.52.0 |
| OS | Windows 11 |

**Local LDAP test target:** Two options:

- **Option A (manual):** Set `LDAP__Host`, `LDAP__BaseDn`, `LDAP__ServiceAccountUser`, `LDAP__ServiceAccountPassword` env vars pointing at any live DC (Windows Server eval or Samba AD container).
- **Option B (auto-provisioned EC2):** Copy `tests/CoreApi.IntegrationTests/appsettings.Development.template.json` → `appsettings.Development.json`, set `TestInfrastructure:ProvisionAdDc: true`, fill in `AmiId`, `SecurityGroupId`, `AdAdminPassword`. The `AdDcProvisionerFixture` launches a Windows Server EC2 instance, installs AD DS via UserData, waits for LDAP port 389, runs tests, then stops the instance. Subsequent runs reuse the stopped instance via `ExistingInstanceId`.

`tests/**/appsettings.Development.json` is gitignored — never commit it.

**AWS deployment target:** Not yet decided (ECS / EKS / Beanstalk). Finalize before Spec 9.

---

## Conventions

### C# naming

| Element | Convention | Example |
| --- | --- | --- |
| Classes, methods, properties | PascalCase | `GetUserAsync` |
| Private fields | `_camelCase` | `_ldapConnection` |
| Interfaces | `IPascalCase` | `IDirectoryConnection` |
| Constants | PascalCase | `DefaultPageSize` |
| Async methods | Suffix `Async` | `CreateUserAsync` |

### Folder layout

```text
src/
  CoreApi/
    Controllers/          — HTTP layer only; no business logic
    Services/             — one service per AD object type
    Infrastructure/       — LDAP connection, low-level AD operations
    Models/               — request/response DTOs
    Hooks/                — Spec 8 cross-cutting business logic
tests/
  CoreApi.UnitTests/      — no AD connectivity required
  CoreApi.IntegrationTests/ — requires real LDAP target
```

### Test categorization

```csharp
[Trait("Category", "Unit")]        // unit tests — no AD required
[Trait("Category", "Integration")] // integration tests — real LDAP required
```

Run unit only: `dotnet test --filter "Category=Unit"`
Run integration: `dotnet test --filter "Category=Integration"`

A test in `IntegrationTests/` without the `Integration` trait is a configuration error.

---

## Skills

- `/spec-done` — runs the Definition of Done checklist for the current spec
- `/run-integration-tests` — runs integration tests against the real LDAP target

---

## Specs

Each spec is a self-contained unit of work. Implement them in order; later specs build on earlier ones.

### Spec 1 — Project scaffold
Empty ASP.NET Core Web API project, solution structure, folder conventions, `.gitignore`, `appsettings` skeleton (connection strings, JWT config placeholders).

### Spec 2 — AD DS connection layer
`IDirectoryConnection` abstraction + implementation using `System.DirectoryServices.Protocols`.
Config: host, port, base DN, service account credentials, TLS toggle.
No business logic in this layer.

### Spec 3 — JWT authentication middleware
Bearer token validation with configurable `Authority`, `Audience`, `Issuer` from `appsettings`.
Fail-fast on startup if required config is missing. No AD involvement.

Swagger contribution: add `AddSecurityDefinition("Bearer", ...)` + `AddSecurityRequirement(...)` to `AddSwaggerGen` so the Swagger UI shows an **Authorize** button. Every protected endpoint must show the padlock icon. Verify manually in Swagger UI that providing a valid token unlocks calls and an invalid token returns 401.

### Spec 4 — User CRUD
Create / Read / Update / Delete AD **user** objects via the Spec 2 layer.
Scope: standard user accounts only (not service accounts).
No ACL operations yet.

Swagger contribution: enable XML doc generation in `CoreApi.csproj` (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`); wire doc file into `AddSwaggerGen`. Every controller method must have `/// <summary>`, `/// <param>`, and `/// <returns>`. Every action must declare `[ProducesResponseType]` for all returned HTTP status codes (200/201, 400, 401, 403, 404, 409, 503). Tag: `[ApiExplorerSettings(GroupName = "Users")]`.

### Spec 5 — Service account CRUD
Create / Read / Update / Delete AD **service account** objects via the Spec 2 layer.
Handled separately from users: different OU placement, naming conventions, and attribute sets.
No ACL operations yet.

Swagger contribution: same XML doc and `[ProducesResponseType]` rules as Spec 4. Tag: `[ApiExplorerSettings(GroupName = "ServiceAccounts")]`. Document the `servicePrincipalName` field explicitly — it is a multi-value attribute with a specific format (`ServiceClass/FQDN:Port`) that callers must follow.

### Spec 6 — Group & OU CRUD
Create / Read / Update / Delete AD group and organizational unit objects via the Spec 2 layer.

Swagger contribution: same XML doc and `[ProducesResponseType]` rules. Tags: `[ApiExplorerSettings(GroupName = "Groups")]` and `[ApiExplorerSettings(GroupName = "OUs")]`. Document the `groupType` field with all valid scope × type combinations. Document that OU delete with `force=false` fails if the OU is protected — callers must know to set `force=true` to remove the protection DENY ACE first.

### Spec 7 — ACL management
Read and write DACLs on AD objects using `System.Security.AccessControl`.
Applies to any object type (users, service accounts, groups, OUs).

Swagger contribution: same XML doc and `[ProducesResponseType]` rules. Tag: `[ApiExplorerSettings(GroupName = "ACL")]`. Document the ACE model in `<remarks>` — rights enum values, ObjectType GUID meaning, inheritance flags. Include at least one request example showing a `Reset Password` extended right grant.

### Spec 8 — Business logic hooks
Extensibility point for cross-cutting rules (e.g. "add user to default group on creation").
Wired into the CRUD layers from Specs 4–6.

### Spec 9 — AWS deployment config
`Dockerfile` + health check endpoint (`GET /health`).
Note: Kerberos on Linux containers requires domain-join or keytab configuration — finalize once AWS deployment target (ECS / EKS / Beanstalk) is chosen.

### Spec 10 — Swagger conformity audit & UI coherence

Final end-to-end review of the generated OpenAPI surface. No new AD or business logic — this spec only adds structure, examples, and validation. Not complex by build gate criteria.

**Deliverables:**

1. **Validate OpenAPI document** — run `dotnet tool run swagger tofile` (or Swashbuckle CLI) and validate the output JSON against the OpenAPI 3.x schema. Zero validation errors.

2. **Tag coherence** — confirm all endpoints appear under the correct group tags (Users, ServiceAccounts, Groups, OUs, ACL) with no stray ungrouped endpoints. Tags must be declared globally in `AddSwaggerGen` with a description for each.

3. **Response code completeness** — every endpoint must document 401, 403, and 503 in addition to its success code and 400/404 where applicable. No undeclared status codes in responses.

4. **Error schema conformity** — confirm every non-2xx response in the OpenAPI document references the `ProblemDetails` schema (RFC 7807). No raw `string` or anonymous error bodies.

5. **Request/response examples** — add at least one `[SwaggerRequestExample]` / `[SwaggerResponseExample]` per resource group (Users, Groups, OUs, ACL). Examples must use realistic AD data (real DN formats, valid UPN patterns, correct `userAccountControl` values).

6. **Auth flow end-to-end** — open Swagger UI, click Authorize, paste a JWT, call one protected endpoint per group, verify 200 is returned. Document the test token source (local dev IdP or static test token) in `appsettings.Development.json`.

7. **Naming consistency audit** — verify that DTO property names follow camelCase in JSON, that all route segments follow kebab-case conventions, and that no synonym pairs exist (e.g., `userName` vs `username` vs `samAccountName` must be consistent across all DTOs).

8. **No breaking route changes** — confirm no route added in Specs 4–9 conflicts with another or shadows `/health`.

9. **Scalar UI** — replace SwaggerUI with [Scalar](https://scalar.com) (`Scalar.AspNetCore` package) for a modern, production-ready API reference experience. Swagger JSON endpoint (`/swagger/v1/swagger.json`) stays unchanged; only the HTML viewer changes.

---

## Evaluation criteria

### Correctness

- LDAP operations verified against a **real AD DS instance** — no mocking the LDAP layer. Mock/prod divergence has caused production failures; integration tests must hit a real target (Windows Server eval or Samba AD container).
- ACL read/write results cross-checked against PowerShell `Get-Acl` / `Set-Acl` output on the same object.
- All REST error responses conform to **RFC 7807** (Problem Details for HTTP APIs) — consistent shape, no naked exception messages.
- JWT validation explicitly rejects: expired tokens, wrong audience, wrong issuer, tampered signature, `alg: none`.

### Security

- Zero credentials in source code or log output — verified by static scan before every merge.
- LDAPS enforced in all non-development configurations (`TLS toggle` from Spec 2 must default to `true`).
- All LDAP search filters use parameterized construction — no string concatenation (LDAP injection prevention).
- JWT algorithm explicitly allowlisted in config; `alg: none` must be rejected at the middleware level.
- Service account AD permissions documented and scoped to minimum required — verified manually against the AD DS target before Spec 9 ships.

### Reliability

- Every AD operation has an explicit timeout configured — no indefinite blocking calls.
- LDAP connection failure surfaces as `503 Service Unavailable` with a `Retry-After` header, not a `500`.
- `GET /health` reports AD connectivity as a named sub-check (separate from app liveness).

### API contract

- OpenAPI spec is **auto-generated from code** (Swashbuckle) — no hand-maintained spec files that can drift.
- All endpoints versioned under `/v1/` from Spec 1 — breaking changes require a new version prefix.
- Every endpoint returns a consistent error envelope; no endpoint may return a raw exception body.
- Every controller action has `/// <summary>` XML documentation and `[ProducesResponseType]` for every returned status code.
- Swagger UI is always enabled (all environments) and usable as an interactive test tool — JWT Authorize button present from Spec 3 onward.
- Spec 10 validates the full OpenAPI surface for conformity and coherence before the project is considered complete.

### Test coverage

- Unit tests cover all business logic hooks (Spec 8) without requiring AD connectivity.
- Integration tests for Specs 2–7 run against a real LDAP target; mocking `IDirectoryConnection` is forbidden in integration tests.
- A test that passes against a mock but not a real AD instance is considered a failing test.

### Observability

- Every request produces a structured JSON log entry including a correlation ID traceable across caller APIs.
- No PII in logs at the default log level — DN values, display names, and passwords must not appear in `Information` or below.

### Build quality

- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in the project file — zero compiler warnings at merge time.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`).
- All NuGet dependencies pinned via a lock file (`packages.lock.json`).

---

## Definition of Done (applies to every spec)

A spec is complete only when ALL of the following are true:

- All new code compiles with zero warnings (`dotnet build -warnaserror`)
- Unit tests pass (`dotnet test`)
- Integration tests pass against a real LDAP target (not a mock)
- OpenAPI spec has been regenerated and committed
- No secrets appear in staged files (verified by `git diff --cached`)
- Every new controller action has `/// <summary>` XML doc and `[ProducesResponseType]` for all returned status codes (Specs 4–7 and 10)
- For complex specs (2, 7, 8, 9): Codex review completed and both systems agree

---

## Complex build gate

A spec is automatically complex — no judgment required — if it meets **any** of these criteria:

- Touches LDAP wire-protocol code (any use of `LdapConnection` or `SearchRequest`)
- Reads or writes binary ACL structures (any use of `ActiveDirectorySecurity`)
- Configures or validates Kerberos / GSSAPI
- Modifies `Dockerfile` or container entrypoints
- Wires cross-cutting logic that modifies state across multiple AD object types simultaneously

Expected complex specs under this definition: **Spec 2**, **Spec 7**, **Spec 8**, **Spec 9**.

For any complex spec, the completed implementation must be submitted to **OpenAI Codex** for independent review before being marked done. Both systems (Claude and Codex) must agree the implementation is correct, secure, and complete against the spec. Disagreements must be resolved explicitly, not ignored.
