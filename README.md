# coreapi

Core Active Directory Domain Services (AD DS) gateway for the Ouritres organisation. Higher-level topic APIs (user management, group management, etc.) delegate all Active Directory operations to this single service.

## Purpose

- Talks directly to AD DS via LDAP / Kerberos / LDAPS
- Authenticates and authorises its own callers (the topic APIs)
- Exposes a REST API surface to those callers
- Owns cross-cutting business logic (e.g. default group memberships on user creation)

## Architecture

| Concern | Choice |
|---------|--------|
| Language / framework | C# / .NET 9 / ASP.NET Core Web API |
| Caller authentication | JWT Bearer — issuer/authority configurable at deploy time |
| AD DS protocol | `System.DirectoryServices.Protocols` (raw LDAP/LDAPS) |
| Deployment | AWS (target TBD — ECS / EKS / Beanstalk) |
| API documentation | Swashbuckle / OpenAPI 3.x |

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- An AD DS instance (Windows Server eval or Samba AD DC container) for integration tests

### Build

```bash
dotnet build
```

### Unit tests — no AD required

```bash
dotnet test --filter "Category=Unit"
```

### Integration tests — real LDAP target required

Configure the endpoint first (environment variables or `appsettings.Development.json`):

| Variable | Example |
|----------|---------|
| `LDAP__Host` | `dc.corp.local` |
| `LDAP__BaseDn` | `DC=corp,DC=local` |
| `LDAP__Port` | `636` |
| `LDAP__UseTls` | `true` |
| `LDAP__ServiceAccountUser` | `svc-coreapi@corp.local` |
| `LDAP__ServiceAccountPassword` | *(from a secrets manager)* |

```bash
dotnet test --filter "Category=Integration"
```

## Folder layout

```
src/
  CoreApi/
    Controllers/       — HTTP layer only; no business logic
    Services/          — one service per AD object type
    Infrastructure/    — LDAP connection, low-level AD operations
    Models/            — request/response DTOs
    Hooks/             — cross-cutting business logic hooks
tests/
  CoreApi.UnitTests/        — no AD required
  CoreApi.IntegrationTests/ — requires a real LDAP target
```

## Security notes

- LDAPS is enforced in all non-Development environments (`UseTls` defaults to `true`).
- All LDAP search filters use `LdapFilterEncoder.Escape()` — no string concatenation.
- Credentials come from configuration at runtime; no secrets in source.
- JWT `alg: none` and tampered/expired tokens are rejected at middleware level.

## Implementation roadmap

| Spec | Status | Description |
|------|--------|-------------|
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
