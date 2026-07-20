# Matrice de vérification — CoreAPI

Relie chaque spécification à ses preuves réelles (test, commit, ou constat d'absence). Complète [`../specifications/catalog.yml`](../specifications/catalog.yml) (champ `acceptance_evidence`) avec le détail par fichier. Aucune ligne "aucune preuve" n'est laissée ambiguë — elle signifie explicitement que rien n'a été trouvé après recherche.

| Spec | Statut | Tests | Preuve d'exécution | Notes |
|---|---|---|---|---|
| [SPEC-0](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md) | partial | `tests/CoreApi.IntegrationTests/Infrastructure/DirectoryConnectionTests.cs` (3), `.../Services/UserServiceTests.cs` (2) | `.claude/handoff/spec-0-status.md` (RESOLVED 2026-07-16) | Non réexécuté pendant EN-01 |
| [SPEC-1](../specifications/enablers/spec-1-project-scaffold.md) | done | — | `dotnet build` réussit (structure de solution) | Rien à tester dans un scaffold |
| [SPEC-2](../specifications/enablers/spec-2-ldap-connection-layer.md) | done | `LdapDirectoryConnectionTests.cs`, `LdapDirectoryConnectionCertificateTests.cs`, `LdapFilterEncoderTests.cs`, `LdapDnEncoderTests.cs`, `DirectoryConnectionOptionsTests.cs` (unitaires) + `DirectoryConnectionTests.cs` (intégration) | Découverte `--list-tests` le 2026-07-19 | Garde-fou `UseTls` hors Development non testé |
| [SPEC-3](../specifications/functional/spec-3-jwt-authentication.md) | done | `JwtTokenValidationTests.cs` (8 cas), `JwtOptionsTests.cs` | Découverte `--list-tests` le 2026-07-19 | Garde-fou `DevSigningKeyPath` non testé |
| [SPEC-4](../specifications/functional/spec-4-user-crud-and-scopes.md) | done | `UsersControllerAuthorizationTests.cs` (12 cas), `UserServiceTests.cs` (unit + intégration), `ScopePoliciesTests.cs` | Exécuté avec succès sur commit `78a45ac`, fusionné dans `main` via PR #5 (voir [reviews/2026-07-19-architecture-security-review.md](reviews/2026-07-19-architecture-security-review.md) §3.5, correction) ; découverte `--list-tests` le 2026-07-19 | Écart : pas de test HTTP inter-tier |
| [SPEC-5](../specifications/functional/spec-5-service-accounts.md) | not-started | — | — | Aucun code |
| [SPEC-6](../specifications/functional/spec-6-groups-and-ous.md) | not-started | — | — | Aucun code |
| [SPEC-7](../specifications/functional/spec-7-acl-and-delegations.md) | not-started | — | — | Aucun code |
| [SPEC-8](../specifications/functional/spec-8-business-logic-hooks.md) | not-started | — | — | `Hooks/.gitkeep` seul |
| [SPEC-9](../specifications/enablers/spec-9-aws-deployment.md) | not-started | — | — | Aucun `Dockerfile`/CI |
| [SPEC-10](../specifications/functional/spec-10-openapi-and-api-experience.md) | partial | `UsersControllerAuthorizationTests.Swagger_document_lists_users_endpoints_with_their_required_scope` | Découverte `--list-tests` le 2026-07-19 | Pas de test du garde d'environnement (inexistant) |
| EN-01 | done | — | — | Incrément documentaire lui-même — validé explicitement par Philippe le 2026-07-20 (voir `docs/adr/decisions-log.md`) |
| EN-02 à EN-10 | planned | — | — | Aucune implémentation |
| SEC-01 à SEC-05 | planned | — | — | Aucune implémentation ; SEC-05 partiellement couvert par les preuves de SPEC-2 pour son volet déjà acquis |
| SPIKE-01 à SPIKE-04 | planned | — | `git diff --stat` exécuté le 2026-07-19 pour SPIKE-03/SPIKE-04 (preuve négative : branches vides) | Aucune investigation menée |

## Total de tests au HEAD actuel (`main` @ `1ff3df4`)

94 unitaires + 5 intégration = **99 cas**, découverts via `dotnet test --list-tests` le 2026-07-19 (pas réexécutés pendant cette réconciliation documentaire). Détail complet, y compris la distinction découverte/exécution et l'attribution correcte de la dernière preuve d'exécution réussie (commit `78a45ac`, fusionné via PR #5) : [`reviews/2026-07-19-architecture-security-review.md`](reviews/2026-07-19-architecture-security-review.md) §3.5.
