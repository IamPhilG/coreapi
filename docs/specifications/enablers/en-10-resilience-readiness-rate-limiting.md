---
id: EN-10
title: Résilience, readiness et rate limiting
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-10 — Résilience, readiness et rate limiting

## Statut réel

**Planned.** Aucune limitation de débit dans le code (`grep` exhaustif sur `src/CoreApi` et `tests/`, aucun résultat). Connexion LDAP singleton sans reconnexion automatique après échec transitoire.

## Implémenté

Aucun.

## Vérifié

Absence confirmée par recherche exhaustive dans le code (pas de middleware `RateLimiting`).

## Dette connue

- Aucune limitation de débit HTTP — exposition à la consommation excessive de ressources (recherches LDAP coûteuses), notamment via les endpoints `/v1/users` non gardés par ailleurs (voir [SPEC-10](../functional/spec-10-openapi-and-api-experience.md)/[EN-04](en-04-secure-demo.md)).
- Pas de logique de reconnexion/health-check actif sur la connexion LDAP singleton.

## Dépendances

- `depends_on` : [SPEC-2](../enablers/spec-2-ldap-connection-layer.md), [SPEC-4](../functional/spec-4-user-crud-and-scopes.md)
- `blocks` : —

## Critères d'acceptation

- Un middleware de limitation de débit (`Microsoft.AspNetCore.RateLimiting`) protège `/v1/*`.
- Une coupure LDAP transitoire est détectée et la connexion est rétablie sans redémarrage du process.

## Preuves

Aucune (planifié).

## Prochaines étapes

Priorité P1 du backlog de la revue de sécurité (avant d'élargir au-delà du Tier 2).
