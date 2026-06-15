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

The test suite can launch (or restart) an EC2 Windows Server instance, install AD DS via UserData, and tear down (stop) the instance automatically at the end of the test run.

**Setup (one time):**

1. Copy the template and fill in your values:

   ```bash
   cp tests/CoreApi.IntegrationTests/appsettings.Development.template.json \
      tests/CoreApi.IntegrationTests/appsettings.Development.json
   ```

2. Edit the copy — set `ProvisionAdDc: true` and supply at minimum:

   | Key | Description |
   | --- | --- |
   | `AwsRegion` | Region where the instance will run |
   | `AmiId` | Windows Server 2022 Base AMI ID for that region |
   | `SecurityGroupId` | SG with inbound TCP 389/636 open to your dev IP |
   | `AdAdminPassword` | Must satisfy Windows Server complexity rules |

3. Ensure AWS credentials are available (environment variables, `~/.aws/credentials`, or IAM role).

4. Run integration tests — the fixture handles the rest:

   ```bash
   dotnet test --filter "Category=Integration"
   ```

5. **After the first run:** copy the `ExistingInstanceId` printed in the test output into your `appsettings.Development.json`. Subsequent runs start the stopped instance (~2 min) instead of launching a new one (~15 min).

> **Elastic IP:** Allocate one in the AWS console and set `ElasticIpAllocationId` so the instance always gets the same IP address. Without it, the IP changes on every start/stop cycle.
>
> **Cost:** A `t3.medium` Windows instance costs ~$0.075/hr while running. The fixture stops (not terminates) the instance after each test run — you pay only for EBS storage (pennies/month) between runs.
>
> **appsettings.Development.json is gitignored** — it contains credentials and is never committed.

#### Demo mode — keep the DC running for a live demo

Set two extra flags in `appsettings.Development.json`:

```json
{
  "TestInfrastructure": {
    "ProvisionAdDc": true,
    "KeepRunning": true,
    "SeedDemoData": true,
    ...
  }
}
```

| Flag | Effect |
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

> **Note on user state:** Users are created disabled because setting a password over plain LDAP (port 389) is forbidden by AD. Enabling accounts requires LDAPS (port 636). For a demo of the CRUD API surface, disabled accounts are sufficient — they are fully visible in directory searches and attribute reads.

**After the test run**, the fixture prints the host and base DN to console. Point the coreapi app at that DC by setting the `DirectoryConnection` block in `appsettings.Development.json` of the main project, then run `dotnet run --project src/CoreApi` and open Swagger UI.

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
