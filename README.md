# coreapi

Core Active Directory Domain Services (AD DS) gateway for the Ouritres organisation. Higher-level topic APIs (user management, group management, etc.) delegate all Active Directory operations to this single service.

## Purpose

- Talks directly to AD DS via LDAP / Kerberos / LDAPS
- Authenticates and authorises its own callers (the topic APIs)
- Exposes a REST API surface to those callers
- Owns cross-cutting business logic (e.g. default group memberships on user creation)

## Architecture

| Concern | Choice |
| --- | --- |
| Language / framework | C# / .NET 9 / ASP.NET Core Web API |
| Caller authentication | JWT Bearer — issuer/authority configurable at deploy time |
| AD DS protocol | `System.DirectoryServices.Protocols` (raw LDAP/LDAPS) |
| Deployment | AWS (target TBD — ECS / EKS / Beanstalk) |
| API documentation | Swashbuckle / OpenAPI 3.x |

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- An AD DS instance for integration tests — see **Integration test target** below

### Build

```bash
dotnet build
```

### Unit tests — no AD required

```bash
dotnet test --filter "Category=Unit"
```

### Integration tests — real LDAP target required

Two ways to provide a target:

#### Option A — Manual (existing DC or Samba container)

Set environment variables before running tests:

| Variable | Example | Notes |
| --- | --- | --- |
| `LDAP__Host` | `dc.corp.local` | IP or hostname of the DC |
| `LDAP__BaseDn` | `DC=corp,DC=local` | Domain root |
| `LDAP__Port` | `389` | 636 for LDAPS |
| `LDAP__UseTls` | `false` | `true` for LDAPS |
| `LDAP__ServiceAccountUser` | `svc-coreapi@corp.local` | |
| `LDAP__ServiceAccountPassword` | *(from a secrets manager)* | Never in source |

```bash
dotnet test --filter "Category=Integration"
```

#### Option B — Auto-provisioned EC2 Windows Server DC

Run the setup wizard once — it handles everything interactively:

```powershell
.\tools\setup-test-dc.ps1
```

The wizard will:

1. Validate your AWS CLI credentials
2. Ask for region, instance type, domain name, and AD administrator password
3. Optionally allocate an Elastic IP (recommended — keeps the same address across restarts)
4. Find the latest Windows Server 2022 Base AMI in your region
5. Create a Security Group `coreapi-test-dc` with inbound TCP 389 / 636 / 3389 from your current IP
6. Ask for the run mode: **test** or **demo** (see below)
7. Write `tests/CoreApi.IntegrationTests/appsettings.Development.json` (gitignored)
8. Optionally run the integration tests immediately

The wizard is idempotent — re-running it reuses existing AWS resources and preserves the `ExistingInstanceId` written after the first test run.

**First run:** ~12–15 min (Windows boot + AD DS promotion + reboot).
**Later runs:** ~2–3 min (restart the stopped instance).

> **Cost:** A `t3.medium` Windows instance costs ~$0.075/hr while running. In test mode the fixture stops (not terminates) the instance after each run — you pay only for EBS storage (pennies/month) between runs.

#### Demo mode

Pass `-Mode demo` to the wizard (or choose `demo` when prompted):

```powershell
.\tools\setup-test-dc.ps1 -Mode demo
```

| Flag set by demo mode | Effect |
| --- | --- |
| `KeepRunning: true` | Fixture skips `StopInstances` on teardown — DC stays up after tests |
| `SeedDemoData: true` | Populates AD with realistic objects before tests run (idempotent) |

**Demo objects seeded automatically:**

| Type | Objects |
| --- | --- |
| OUs | `Users`, `ServiceAccounts`, `Groups` |
| Users | `alice.martin`, `bob.dupont`, `claire.bernard` |
| Groups | `IT-Admins`, `Dev-Team` (global security) |
| Service account | `svc-coreapi` |

After the test run the fixture prints the DC host and base DN. Point the coreapi app at that DC by updating `src/CoreApi/appsettings.Development.json`, then run `dotnet run --project src/CoreApi` and open Swagger UI.

> **Note:** Users are created disabled — enabling them requires LDAPS (port 636) to set a password. For a demo of the CRUD API surface, disabled accounts are sufficient.

## Folder layout

```text
src/
  CoreApi/
    Controllers/       — HTTP layer only; no business logic
    Services/          — one service per AD object type
    Infrastructure/    — LDAP connection, low-level AD operations
    Models/            — request/response DTOs
    Hooks/             — cross-cutting business logic hooks
tests/
  CoreApi.UnitTests/        — no AD required
  CoreApi.IntegrationTests/
    Infrastructure/         — LDAP connection tests
    TestInfrastructure/     — optional EC2 DC provisioner fixture
```

## Security notes

- LDAPS is enforced in all non-Development environments (`UseTls` defaults to `true`).
- All LDAP search filters use `LdapFilterEncoder.Escape()` — no string concatenation.
- Credentials come from configuration at runtime; no secrets in source.
- JWT `alg: none` and tampered/expired tokens are rejected at middleware level.
- `tests/**/appsettings.Development.json` is gitignored — credentials never reach the repo.

## Implementation roadmap

| Spec | Status | Description |
| --- | --- | --- |
| 1 | Done | Project scaffold |
| 2 | Done (unit tests pass; integration tests pending real LDAP) | AD DS connection layer |
| 3 | Pending | JWT authentication middleware |
| 4 | Pending | User CRUD |
| 5 | Pending | Service account CRUD |
| 6 | Pending | Group & OU CRUD |
| 7 | Pending | ACL management |
| 8 | Pending | Business logic hooks |
| 9 | Pending | AWS deployment config |
| 10 | Pending | Swagger conformity audit |
