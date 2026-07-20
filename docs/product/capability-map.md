# Carte des capacités — CoreAPI

Statut réel au 2026-07-19 (HEAD `main` @ `1ff3df4`). Source détaillée : [`specifications/catalog.yml`](../specifications/catalog.yml) et chaque fiche de spec. Aucune capacité n'est indiquée `done` sans preuve exécutable (test) ou lecture de code directe.

| Capacité | Spec | Statut | Ce qu'un appelant peut faire aujourd'hui |
|---|---|---|---|
| Authentification des appelants (JWT Bearer) | [Spec 3](../specifications/functional/spec-3-jwt-authentication.md) | **done** | Présenter un token RS256 valide (iss/aud/exp/alg vérifiés) ; rejeté sinon (401) |
| Gestion des utilisateurs standards (Tier 2) | [Spec 4](../specifications/functional/spec-4-user-crud-and-scopes.md) | **done** | CRUD complet sur `/v1/users`, sous scope `coreapi.ad.t2.users.<verb>`, confiné au sous-arbre `BaseDn` configuré |
| Documentation API interactive (Swagger/OpenAPI) | [Spec 10](../specifications/functional/spec-10-openapi-and-api-experience.md) | **partial** | Le schéma existe et se génère, mais il est exposé sans garde d'environnement — non conforme à un usage sûr en démo/prod tel quel |
| Comptes de service (CRUD) | [Spec 5](../specifications/functional/spec-5-service-accounts.md) | **not-started** | Rien — aucun contrôleur/service n'existe |
| Groupes & OU (CRUD) | [Spec 6](../specifications/functional/spec-6-groups-and-ous.md) | **not-started** | Rien |
| ACL & délégations | [Spec 7](../specifications/functional/spec-7-acl-and-delegations.md) | **not-started** | Rien |
| Hooks de logique métier transverse | [Spec 8](../specifications/functional/spec-8-business-logic-hooks.md) | **not-started** | `Hooks/` ne contient qu'un `.gitkeep` |

## Fondations techniques (enablers déjà en place)

| Fondation | Spec | Statut |
|---|---|---|
| Squelette projet | [Spec 1](../specifications/enablers/spec-1-project-scaffold.md) | **done** |
| Connexion LDAP/LDAPS | [Spec 2](../specifications/enablers/spec-2-ldap-connection-layer.md) | **done** (dette notée) |
| Infra AD de test/démo (EC2 + SSM) | [Spec 0](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md) | **partial** |
| Déploiement AWS | [Spec 9](../specifications/enablers/spec-9-aws-deployment.md) | **not-started** (cible candidate uniquement) |

## Ce qui n'est explicitement pas acquis

- Les branches `feature/integration-record-cert-binding` et `feature/multi-instance-support` **ne contiennent aucun travail réel** (diff contre `main` = suppressions uniquement) — ce sont des marqueurs d'intention pour [SPIKE-04](../specifications/spikes/spike-04-integration-record-cert-binding.md) et [SPIKE-03](../specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md), pas des fonctionnalités en cours.
- ECS Fargate Linux est une cible **candidate**, pas décidée — voir [SPIKE-01](../specifications/spikes/spike-01-ecs-fargate-linux-viability.md).
- Aucun tier autre que Tier 2 n'est implémenté ; aucune autorisation par objet/attribut n'existe — voir [SEC-01](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md).
