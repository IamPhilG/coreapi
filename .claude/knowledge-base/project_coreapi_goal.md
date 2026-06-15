---
name: project-coreapi-goal
description: Core purpose and confirmed architectural decisions for ouritres/coreapi
metadata:
  type: project
---

`coreapi` is the shared AD DS gateway backend for the Ouritres GitHub org. Higher-level topic APIs (user management, group management, etc.) delegate all AD operations to this single service.

**Scope (all confirmed by user):**
- Talks directly to AD DS via LDAP / Kerberos / LDAPS
- Authenticates and authorizes its own callers (the topic APIs)
- Exposes a REST API surface to those callers
- Owns business logic (e.g. default group memberships on user creation)

**Confirmed architectural decisions:**
- Decision A: C# / .NET 9 / ASP.NET Core Web API
- Decision B: Callers authenticate via JWT Bearer token; issuer/authority is configurable at deploy time (not hardcoded to any provider)
- Deployment: AWS (exact target TBD — ECS/EKS/Beanstalk undecided; Kerberos on Linux containers will need extra config)

**Key libraries expected:**
- `System.DirectoryServices.Protocols` — raw LDAP
- `System.DirectoryServices.AccountManagement` — users/groups
- `System.Security.AccessControl` — ACL management
- ASP.NET Core JWT Bearer middleware — caller auth

**Why:** AD DS is Microsoft-native; .NET first-party libs handle Kerberos and binary ACL structures far better than Python alternatives.
