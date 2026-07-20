---
id: SPEC-4
title: CRUD utilisateurs et scopes
type: functional
status: done
last_reviewed: "2026-07-19"
---

# Spec 4 — CRUD utilisateurs et scopes

## Statut réel

**Done.** Implémenté et fusionné dans `main` en deux temps : CRUD de base (`28291ce feat(spec-4): user CRUD via AD DS`) puis retrofit d'autorisation par scope tiered (`78a45ac feat(authz): enforce tiered OAuth scopes on user endpoints`, fusionné via PR #5 / merge `1ff3df4`). Le README racine affiche encore "Pending" — écart corrigé par cet incrément.

## Implémenté

- `GET/POST/PUT/DELETE /v1/users` — `src/CoreApi/Controllers/UsersController.cs`.
- Scope requis par verbe : `coreapi.ad.t2.users.{read,create,update,delete}` — `src/CoreApi/Infrastructure/Authorization/ScopePolicies.cs`.
- Confinement au sous-arbre `BaseDn` configuré : `ouPath` ne peut pas sortir de `DirectoryConnection:BaseDn` — `src/CoreApi/Services/UserService.cs:130-139` (`EnsureWithinConfiguredBaseDn`/`IsDnWithinBaseDn`).
- Pagination interne (cookie AD, plafond 1000), pas de continuation token exposé au consommateur HTTP.
- DTOs à liste fermée de champs (`CreateUserRequest`/`UpdateUserRequest`/`UserDto`) — pas de mass-assignment, `userAccountControl` jamais piloté par l'appelant.

## Vérifié

- `tests/CoreApi.UnitTests/Controllers/UsersControllerAuthorizationTests.cs` : 12 cas — absence de token → 401 ; scope manquant/non lié → 403 (5 endpoints) ; scope requis présent → non bloqué (5 endpoints) ; document Swagger liste bien les scopes requis.
- `tests/CoreApi.UnitTests/Services/UserServiceTests.cs` : confinement `BaseDn`, y compris rejet d'une tentative de sortie par astuce de suffixe DN.
- `tests/CoreApi.IntegrationTests/Services/UserServiceTests.cs` : cycle de vie complet CRUD contre un DC réel, y compris `ConflictException` sur doublon et `NotFoundException` après suppression.

## Dette connue

- **Écart de preuve inter-tier** : aucun test ne monte un JWT avec un scope d'un autre tier (ex. `coreapi.ad.t1.users.read`) contre un endpoint Tier 2 en HTTP réel — seule la fonction `ScopePolicies.HasScope` est prouvée correcte au niveau unitaire. Voir [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md).
- Aucune autorisation par attribut — un porteur du scope `users.update` peut modifier tous les champs exposés.
- Ces endpoints sont exposés par Swagger sans garde d'environnement (dette croisée avec [SPEC-10](spec-10-openapi-and-api-experience.md) / [EN-04](../enablers/en-04-secure-demo.md)).
- Pas de limitation de débit sur ces endpoints (voir [EN-10](../enablers/en-10-resilience-readiness-rate-limiting.md)).

## Dépendances

- `depends_on` : [SPEC-2](../enablers/spec-2-ldap-connection-layer.md), [SPEC-3](spec-3-jwt-authentication.md)
- `blocks` : [SPEC-5](spec-5-service-accounts.md), [SPEC-6](spec-6-groups-and-ous.md), [SPEC-7](spec-7-acl-and-delegations.md), [SPEC-8](spec-8-business-logic-hooks.md)

## Critères d'acceptation

- CRUD complet fonctionnel contre un DC réel (vérifié par les 5 tests d'intégration).
- Chaque verbe HTTP exige son scope exact ; un scope manquant ou non lié produit 403 (vérifié bout-en-bout HTTP).
- Un `ouPath` hors du `BaseDn` configuré est rejeté (vérifié, y compris contre une tentative d'évasion).

## Preuves

- `tests/CoreApi.UnitTests/Controllers/UsersControllerAuthorizationTests.cs`
- `tests/CoreApi.UnitTests/Services/UserServiceTests.cs`
- `tests/CoreApi.IntegrationTests/Services/UserServiceTests.cs`
- Commits `28291ce`, `78a45ac` (fusionné via PR #5)

## Prochaines étapes

- Ajouter le cas de test HTTP inter-tier manquant (voir backlog P0 de la revue de sécurité).
- Autorisation par attribut avant l'ajout d'un tier plus sensible ([SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md)).
