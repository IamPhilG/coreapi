---
id: SPEC-3
title: Authentification JWT
type: functional
status: done
last_reviewed: "2026-07-20"
---

# Spec 3 — Authentification JWT

*Réconcilie `.wip/docs/specs/spec-3-jwt-authentication.md` (conservé tel quel comme source de conception) avec le code fusionné dans `main`.*

## Statut réel

**Done.** Implémenté, fusionné dans `main` (commit `0f08fbf feat(spec-3): JWT Bearer authentication middleware`), et couvert par test. Le README racine affiche encore ce statut comme "Pending" — c'est un écart documentaire connu, corrigé par cet incrément (voir [`../../adr/decisions-log.md`](../../adr/decisions-log.md) et la revue de sécurité du 2026-07-19).

## Implémenté

- Validation JWT Bearer complète : issuer, audience, durée de vie, algorithme (liste blanche RS256 uniquement), signature — `src/CoreApi/Program.cs`, `src/CoreApi/Infrastructure/JwtOptions.cs`.
- `MapInboundClaims = false` pour préserver les noms de claims `scp`/`scope` bruts.
- Garde-fou de démarrage : `DevSigningKeyPath` (clé locale de développement) provoque un échec au démarrage (`ValidateOnStart()`) s'il est activé hors `Development`.

## Vérifié

- `tests/CoreApi.UnitTests/Infrastructure/JwtTokenValidationTests.cs` (8 cas) : token valide, expiré, mauvaise audience, mauvais issuer, signature altérée, `alg:none` non signé, algorithme hors liste blanche, clé non fiable — tous rejetés comme attendu.
- `tests/CoreApi.UnitTests/Controllers/UsersControllerAuthorizationTests.cs::List_without_a_token_returns_401` : rejet HTTP réel confirmé pour l'absence de token.
- Le garde-fou `DevSigningKeyPath` lui-même (échec au démarrage hors Development) n'est **pas** couvert par un test automatisé — implémenté, non vérifié par exécution.

## Dette connue

- Support d'un seul Authority à la fois — le support simultané de plusieurs IdP de confiance (Entra ID + Okta) est demandé (issue GitHub [`#2`](https://github.com/IamPhilG/coreapi/issues/2)) mais non implémenté.
- Pas de sender-constraining (mTLS/DPoP) — risque accepté explicitement pour le Tier 2 actuel, à revisiter avant l'ajout de tiers plus sensibles (voir [SEC-04](../security/sec-04-tiers-jit-privileged-paths.md)).
- Aucun test automatisé du garde-fou `DevSigningKeyPath`.

## Preuve de vérification live (Okta)

*Migré depuis `.wip/docs/specs/spec-3-jwt-authentication.md`.*

Le chemin `options.Authority` (découverte OIDC + récupération JWKS en direct — celui emprunté par tout déploiement réel) est structurellement le seul que les tests unitaires ne peuvent pas couvrir, puisqu'ils exercent le chemin `DevSigningKeyPath` (clé statique locale) à la place. Il a été vérifié une fois, en direct, contre un vrai fournisseur Okta :

- **Org** : Okta Integrator Free Plan (`https://integrator-2848612.okta.com`).
- **Authorization Server `default`** : ne porte aucune Access Policy par défaut sur ce plan gratuit (contrairement aux organisations Workforce payantes) — une policy a dû être créée manuellement avant qu'une requête de token `client_credentials` n'aboutisse.
- **Scope custom `coreapi.access`** : créé sous l'onglet Scopes du serveur `default` — le grant Client Credentials ne peut pas utiliser les scopes OIDC par défaut (`openid`/`profile`), il en faut un dédié.
- **`Jwt:Authority`/`Jwt:Issuer`** : `https://integrator-2848612.okta.com/oauth2/default`. **`Jwt:Audience`** : `api://default`.
- **Piège relevé** : les nouvelles apps Okta Service (API Services) exigent DPoP par défaut depuis une publication Okta de février 2026 (erreur `invalid_dpop_proof`). Désactivé explicitement dans les paramètres généraux de l'app ("Require Demonstrating Proof of Possession (DPoP) header") pour utiliser des Bearer tokens classiques, conformément à ce que cette spec implémente. DPoP (tokens sender-constrained) lui-même n'est pas implémenté — serait une fonctionnalité séparée si jamais requis (voir dette ci-dessus).
- **Méthode de vérification** : une application console de test (non committée) a utilisé `ConfigurationManager<OpenIdConnectConfiguration>` avec `OpenIdConnectConfigurationRetriever` — le même mécanisme que celui utilisé en interne par `AddJwtBearer` pour `options.Authority` — pour récupérer en direct la découverte OIDC + JWKS de l'org Okta ci-dessus, puis a exécuté `JwtSecurityTokenHandler.ValidateToken` contre un vrai token d'accès émis par `client_credentials`, avec exactement la forme de `TokenValidationParameters` que configure `Program.cs`. **Résultat : succès** — un vrai token Okta validé de bout en bout contre des clés de signature récupérées en direct.

## Outillage — `DevTokenMinter`

*Migré depuis `.wip/docs/specs/spec-3-jwt-authentication.md`. Voir aussi [`../enablers/en-04-secure-demo.md`](../enablers/en-04-secure-demo.md) pour son extension prévue en outil de démonstration.*

CLI console séparée (`tools/DevTokenMinter`, jamais référencée par `CoreApi.csproj`) — génère une paire de clés RSA-2048 au premier lancement (`dev-signing-key.*.pem`, gitignorées), émet des tokens pour 6 profils : `valid`, `expired`, `wrong-audience`, `wrong-issuer`, `unsigned` (`alg: none`), `tampered`. Permet d'exercer Spec 3 (Authorize Swagger, `curl`) sans dépendance externe. Usage : `dotnet run -- <profil> [issuer] [audience]` depuis `tools/DevTokenMinter/`.

`AuthorizeCheckOperationFilter` (filtre Swashbuckle `IOperationFilter`) ajoute l'exigence de sécurité Bearer (icône cadenas) uniquement aux opérations dont le contrôleur/action porte `[Authorize]` (et pas `[AllowAnonymous]`) — pour que `/health` ne montre pas à tort une exigence de token. **Note technique** : Microsoft.OpenApi v2 (livré par Swashbuckle.AspNetCore 10.x) a restructuré ses espaces de noms — `Microsoft.OpenApi.Models` est devenu `Microsoft.OpenApi`, et `OpenApiSecurityScheme.Reference` est devenu un type dédié `OpenApiSecuritySchemeReference(id, document)`. `OperationFilterContext.Document` fournit l'`OpenApiDocument` nécessaire pour le construire.

## Dépendances

- `depends_on` : [SPEC-1](../enablers/spec-1-project-scaffold.md)
- `blocks` : [SPEC-4](spec-4-user-crud-and-scopes.md)

## Critères d'acceptation

- Un token invalide (signature, issuer, audience, expiration, algorithme) est rejeté par 401.
- Un token valide au format attendu passe la validation et expose les claims `scp`/`scope` sans transformation.

## Preuves

- `tests/CoreApi.UnitTests/Infrastructure/JwtTokenValidationTests.cs`
- `src/CoreApi/Program.cs:59-99`, `src/CoreApi/Infrastructure/JwtOptions.cs`
- Commit `0f08fbf`

## Prochaines étapes

- Support multi-IdP (issue #2) — non planifié dans cet incrément.
- Ajouter un test automatisé pour le garde-fou `DevSigningKeyPath`.
