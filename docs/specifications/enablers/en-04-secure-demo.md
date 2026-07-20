---
id: EN-04
title: Démonstration sécurisée
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-04 — Démonstration sécurisée

## Statut réel

**Planned.** Aucune implémentation. Conception déjà détaillée dans la revue de sécurité du 2026-07-19 (§9, révision 2) — non dupliquée ici.

## Implémenté

Aucun. `tools/DevTokenMinter` existe déjà comme CLI séparé (profils `valid/expired/wrong-audience/wrong-issuer/unsigned/tampered`) mais n'a pas encore les profils métier nécessaires (`demo-reader`, `demo-user-operator`, etc.) ni de garde d'affichage Swagger dédié.

## Vérifié

Aucun.

## Dette connue

Swagger/OpenAPI est aujourd'hui exposé **sans aucune condition d'environnement** (voir [SPEC-10](../functional/spec-10-openapi-and-api-experience.md)) — préalable bloquant à traiter avant tout mode démo.

## Dépendances

- `depends_on` : [SPEC-4](../functional/spec-4-user-crud-and-scopes.md), [SPEC-10](../functional/spec-10-openapi-and-api-experience.md)
- `blocks` : —

## Critères d'acceptation (contrainte non négociable)

- **CoreAPI reste strictement un resource server — il n'émet, ne signe et ne distribue jamais de token lui-même.** Le composant de signature de jetons de démo reste un outil/service séparé (`tools/DevTokenMinter`), jamais un endpoint de `CoreApi.csproj`.
- Disponible uniquement en `Development` ou `Demo` explicitement isolé ; option dédiée désactivée par défaut ; échec au démarrage si activée ailleurs.
- Six profils prédéfinis à liste fermée (`demo-reader`, `demo-user-operator`, `demo-user-admin`, `demo-denied`, `demo-wrong-tier`, `demo-expired`) — aucune saisie libre de claims.
- Bannière visible « DEMO — IDENTITÉS ET DONNÉES FICTIVES ».
- `[Authorize]` et la validation JWT réelle ne sont jamais contournés.

## Preuves

Aucune (planifié).

## Prochaines étapes

Voir le détail complet de conception (comparaison de variantes, étapes) dans [`../../assurance/reviews/2026-07-19-architecture-security-review.md`](../../assurance/reviews/2026-07-19-architecture-security-review.md) §9.
