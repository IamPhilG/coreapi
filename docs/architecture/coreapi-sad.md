# CoreAPI — Solution Architecture Document (SAD)

Document maître d'architecture. Il renvoie vers les documents détaillés plutôt que de dupliquer leur contenu — les décisions de conception fines vivent dans [`views/authorization-model.md`](views/authorization-model.md) (modèle d'autorisation consolidé, document canonique) et dans le catalogue de spécifications. `.wip/docs/architecture/*.md` a servi de source historique à cette consolidation (voir provenance dans `authorization-model.md`) mais n'est plus la référence à lire pour comprendre le modèle actuel.

État de référence : `main` @ `1ff3df4`. Dernière réconciliation : 2026-07-19 (incrément EN-01).

## 1. Contexte business et produit

Voir [`../product/vision.md`](../product/vision.md) et [`../product/capability-map.md`](../product/capability-map.md). En bref : CoreAPI est la passerelle REST unique vers AD DS pour l'organisation Ouritres ; les API topiques lui délèguent toutes les opérations Active Directory.

## 2. Architecture de la solution

### Composants (`src/CoreApi/`)

| Composant | Rôle | Fichier |
|---|---|---|
| `Program.cs` | pipeline ASP.NET Core : JWT bearer, policies d'autorisation, Swagger, health check | `src/CoreApi/Program.cs` |
| `Controllers/UsersController.cs` | CRUD HTTP `/v1/users`, un `[Authorize(Policy=...)]` par verbe | `src/CoreApi/Controllers/UsersController.cs` |
| `Infrastructure/Authorization/ScopePolicies.cs` | format de scope `coreapi.ad.<tier>.<resource>.<verb>` | `src/CoreApi/Infrastructure/Authorization/ScopePolicies.cs` |
| `Infrastructure/LdapDirectoryConnection.cs` | connexion LDAP singleton, TLS, validation certificat/hostname, pagination, annulation | `src/CoreApi/Infrastructure/LdapDirectoryConnection.cs` |
| `Infrastructure/LdapFilterEncoder.cs` / `LdapDnEncoder.cs` | anti-injection RFC 4515 / RFC 4514 | idem |
| `Services/UserService.cs` | logique métier, confinement au sous-arbre `BaseDn` configuré | `src/CoreApi/Services/UserService.cs` |
| `Infrastructure/ProblemDetailsExceptionHandler.cs` | mapping exceptions → RFC 7807 | `src/CoreApi/Infrastructure/ProblemDetailsExceptionHandler.cs` |
| `tools/DevTokenMinter` | CLI séparé de génération de JWT de test — jamais référencé par `CoreApi.csproj` | `tools/DevTokenMinter/Program.cs` |
| `tools/setup-test-dc.ps1` + `TestInfrastructure/AdDcProvisionerFixture.cs` | provisionnement EC2 Windows + promotion AD DS pour tests d'intégration | voir [Spec 0](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md) |

### Vues détaillées

- [`views/context-and-trust-boundaries.md`](views/context-and-trust-boundaries.md) — contexte système et frontières de confiance
- [`views/identity-and-authorization-flow.md`](views/identity-and-authorization-flow.md) — flux d'authentification/autorisation
- [`views/deployment-view.md`](views/deployment-view.md) — état actuel du déploiement (aucune cible décidée)

### Point d'architecture notable

CoreAPI n'effectue **aucune délégation d'identité** vers AD — toutes les opérations LDAP s'exécutent sous l'identité du compte de service configuré, jamais sous celle de l'appelant JWT. CoreAPI est donc de facto le seul point de contrôle d'accès : toute faille dans la vérification de scope équivaut à un accès direct avec les pleins pouvoirs du compte de service sur le sous-arbre configuré. Voir [SEC-02](../specifications/security/sec-02-execution-identities-secrets-least-privilege.md).

## 3. Données et contrats

DTOs actuels (`src/CoreApi/Models/`) : `CreateUserRequest`, `UpdateUserRequest`, `UserDto` — liste fermée de champs (pas de `userAccountControl` ni `memberOf` exposés directement, pas de mass-assignment). Aucun contrat OpenAPI figé/versionné au-delà du préfixe `/v1` ; pas de politique de dépréciation documentée. Voir [Spec 10](../specifications/functional/spec-10-openapi-and-api-experience.md).

## 4. Infrastructure et déploiement

Aucune cible de déploiement de production n'est décidée aujourd'hui (contradiction documentaire en cours de correction — voir [`../assurance/reviews/2026-07-19-architecture-security-review.md`](../assurance/reviews/2026-07-19-architecture-security-review.md) §7). ECS Fargate Linux est une cible **candidate**, conditionnelle à [SPIKE-01](../specifications/spikes/spike-01-ecs-fargate-linux-viability.md) et [EN-08](../specifications/enablers/en-08-aws-deployment-poc.md). Aucun `Dockerfile`, aucune pipeline CI/CD n'existe dans ce dépôt à ce jour.

L'infrastructure AD de test/démo (EC2 + SSM Run Command + `Install-ADDSForest`) est décrite dans [Spec 0](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md) — c'est de l'infrastructure de test, pas de production.

## 5. Sécurité

Le modèle de sécurité complet (sept briques de classification, tiering EAM, Access modality vs Privilege class, PAM et multi-segment, taxonomie des scopes, trois profils d'exécution, stratégie de credential/secretless, fiche d'intégration, contrôle à deux jetons, format de log SIEM, état réel par composant) est consolidé sans perte dans [`views/authorization-model.md`](views/authorization-model.md) — document canonique, qui remplace la référence aux deux documents de conception d'origine (`.wip/docs/architecture/ad-ds-governance-model.md` et `.wip/docs/architecture/authorization-and-access-model.md`, conservés en l'état sous `.wip/` comme historique, non modifiés). Ce chapitre en résume l'état d'implémentation réel :

- **Implémenté et vérifié** : authentification JWT (RS256, allow-list d'algorithmes, iss/aud/exp), autorisation par scope Tier 2 (`coreapi.ad.t2.users.*`), confinement au sous-arbre `BaseDn` configuré, anti-injection LDAP/DN.
- **Non implémenté** : autorisation par objet/attribut ([SEC-01](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md)), Tier 1/Tier 0/JIT ([SEC-04](../specifications/security/sec-04-tiers-jit-privileged-paths.md)), audit structuré/corrélation ([SEC-03](../specifications/security/sec-03-audit-correlation-non-repudiation.md)), trois profils d'exécution complets, suivi `jti` partagé ([SPIKE-03](../specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md)).
- Modèle de menaces complet : [`../assurance/threat-model.md`](../assurance/threat-model.md).

## 6. Intégrations

- **Okta** (Authority OIDC) — validation JWT uniquement, aucune émission de token par CoreAPI.
- **AD DS** — LDAP/LDAPS, compte de service unique.
- **AWS** — test infra uniquement à ce jour (voir §4).

Détail : [`../product/repository-dependencies.md`](../product/repository-dependencies.md).

## 7. Exploitation et observabilité

État actuel quasi nul : un health check générique (`AddHealthChecks()`) qui ne vérifie pas la connectivité AD réelle ; logs `ILogger` ponctuels (certificats, pagination) sans piste d'audit structurée ; pas de SIEM. Voir [EN-09](../specifications/enablers/en-09-observability-and-audit.md), [EN-10](../specifications/enablers/en-10-resilience-readiness-rate-limiting.md), et [`../operations/README.md`](../operations/README.md).

## 8. Qualité et assurance

99 cas de test au HEAD actuel (94 unitaires + 5 intégration), détail complet dans [`../assurance/reviews/2026-07-19-architecture-security-review.md`](../assurance/reviews/2026-07-19-architecture-security-review.md) §3.5. Aucune CI/CD n'exécute ces tests automatiquement — voir [EN-03](../specifications/enablers/en-03-ci-quality-gates.md). Matrice de vérification par spec : [`../assurance/verification-matrix.md`](../assurance/verification-matrix.md).

## 9. Décisions, risques et trajectoire

- Décisions actées : [`../adr/decisions-log.md`](../adr/decisions-log.md)
- Registre de risques priorisé et backlog : [`../assurance/reviews/2026-07-19-architecture-security-review.md`](../assurance/reviews/2026-07-19-architecture-security-review.md) §6/§11
- Trajectoire produit : [`../product/roadmap.md`](../product/roadmap.md)
