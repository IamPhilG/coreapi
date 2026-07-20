# Vue de contexte et frontières de confiance

*Reprise du contexte système établi dans la revue d'architecture/sécurité du 2026-07-19 (§3.1).*

```mermaid
flowchart LR
    subgraph Callers["Topic APIs (appelants)"]
        TA["Topic API (ex: user-mgmt)"]
    end
    subgraph IdP["Identity Provider"]
        OKTA["Okta / futur Entra ID (issue #2 ouverte)"]
    end
    subgraph CoreAPI["CoreAPI (ASP.NET Core, .NET 9)"]
        API["REST /v1/users"]
    end
    subgraph AD["Active Directory Domain Services"]
        DC["DC (test: EC2 Windows Server, AWS)"]
    end

    TA -- "JWT Bearer (scope coreapi.ad.t2.users.*)" --> API
    OKTA -- "émet le token" --> TA
    API -- "LDAP/LDAPS (389/636)" --> DC
    API -- "valide signature/iss/aud via Authority ou clé locale (dev)" --> OKTA
```

## Frontières de confiance

1. **Topic API ↔ CoreAPI** (JWT Bearer) — non authentifié → 401, vérifié par test (`UsersControllerAuthorizationTests.List_without_a_token_returns_401`).
2. **CoreAPI ↔ AD DS** (LDAP/LDAPS, compte de service) — compte de service unique, aucune délégation de l'identité de l'appelant original vers AD.
3. **Opérateur ↔ AWS** (SSM Run Command, IAM) — test infra uniquement, voir [Spec 0](../../specifications/enablers/spec-0-test-demo-ad-infrastructure.md).

## Actifs et acteurs

Voir le modèle de menaces complet : [`../../assurance/threat-model.md`](../../assurance/threat-model.md).
