---
id: SPEC-10
title: OpenAPI et expérience API
type: functional
status: partial
last_reviewed: "2026-07-20"
---

# Spec 10 — OpenAPI et expérience API

## Statut réel

**Partial.** Swashbuckle/OpenAPI est intégré et génère un schéma fonctionnel, mais aucun audit de conformité n'a été mené et le schéma est exposé sans garde d'environnement — ce qui constitue en soi un écart de sécurité (voir la revue de sécurité du 2026-07-19, risque R1).

## Implémenté

- `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI` — `src/CoreApi/Program.cs`.
- `AuthorizeCheckOperationFilter` (`src/CoreApi/Infrastructure/Conventions/AuthorizeCheckOperationFilter.cs`) : ajoute l'annotation Bearer et le scope requis sur chaque action `[Authorize]`.
- `SlugifyParameterTransformer` pour des routes cohérentes.

## Vérifié

- `UsersControllerAuthorizationTests.Swagger_document_lists_users_endpoints_with_their_required_scope` : le document Swagger généré liste bien les endpoints `/v1/users` avec leur scope requis.
- **Non vérifié** : aucun test ne confirme que Swagger est inaccessible en dehors de `Development` — et pour cause, ce garde n'existe pas dans le code actuel (`Program.cs:18,134-135` : `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI` inconditionnels).

## Dette connue

- **Swagger/OpenAPI exposé sans garde d'environnement** — si déployé tel quel, le schéma complet (y compris les annotations de scope requis) serait publiquement accessible. Sévérité : élevée aujourd'hui, critique si un déploiement partagé/public existe. Voir [EN-04](../enablers/en-04-secure-demo.md).
- Aucun audit de conformité OpenAPI mené (cohérence des codes de retour, des schémas d'erreur RFC 7807, etc.).
- Aucune politique de versionnement/dépréciation documentée au-delà du préfixe `/v1`.

## Dépendances

- `depends_on` : [SPEC-4](spec-4-user-crud-and-scopes.md)
- `blocks` : [EN-04](../enablers/en-04-secure-demo.md) (la démo sécurisée a besoin que Swagger soit d'abord gardé par environnement)

## Critères d'acceptation

- Le schéma OpenAPI reflète fidèlement les scopes requis (déjà vérifié).
- Swagger UI n'est accessible qu'en `Development` ou dans un mode `Demo` explicitement isolé et gardé (non vérifié — dépend d'[EN-04](../enablers/en-04-secure-demo.md)).

## Preuves

- `tests/CoreApi.UnitTests/Controllers/UsersControllerAuthorizationTests.cs::Swagger_document_lists_users_endpoints_with_their_required_scope`
- `src/CoreApi/Program.cs:18,134-135` (absence de garde, constat négatif)

## Conception prévue (non implémentée — migrée depuis `.wip/docs/coreapi.md`)

Audit de conformité final prévu pour cette spec, une fois les fondations de sécurité traitées :
1. Validation du document OpenAPI généré contre le schéma OpenAPI 3.x (zéro erreur de validation).
2. Cohérence des tags de groupe (Users, ServiceAccounts, Groups, OUs, ACL), déclarés globalement avec description.
3. Complétude des codes de réponse : 401/403/503 documentés en plus du code de succès et de 400/404 le cas échéant.
4. Conformité du schéma d'erreur : chaque réponse non-2xx référence le schéma `ProblemDetails` (RFC 7807), pas de corps d'erreur `string` brut ou anonyme.
5. Exemples de requête/réponse par groupe de ressources, avec données AD réalistes (formats DN, UPN, valeurs `userAccountControl` valides).
6. Vérification bout-en-bout du flux d'autorisation dans Swagger UI (Authorize → coller un JWT → appel protégé → 200).
7. Audit de cohérence de nommage : camelCase JSON, kebab-case des segments de route, absence de synonymes divergents (`userName` vs `username` vs `samAccountName`).
8. Absence de collision de route entre les specs 4–9, et avec `/health`.
9. **Remplacement prévu de Swagger UI par [Scalar](https://scalar.com)** (`Scalar.AspNetCore`) pour une expérience de référence API plus moderne — l'endpoint JSON `/swagger/v1/swagger.json` resterait inchangé, seul le visualisateur HTML changerait. Cette piste n'est pas priorisée dans le catalogue actuel — à confirmer avant de la planifier, en tenant compte du garde d'environnement requis par [EN-04](../enablers/en-04-secure-demo.md) qui devra s'appliquer identiquement au nouveau visualisateur.

## Prochaines étapes

Gate Swagger par environnement (priorité P0 du backlog de sécurité) avant toute démo externe — voir [EN-04](../enablers/en-04-secure-demo.md).
