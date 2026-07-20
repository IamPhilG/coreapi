---
id: SPEC-8
title: Hooks et règles métier
type: functional
status: not-started
last_reviewed: "2026-07-19"
---

# Spec 8 — Hooks et règles métier

## Statut réel

**Not-started.** Le dossier `src/CoreApi/Hooks/` existe dans le squelette du projet mais ne contient qu'un fichier `.gitkeep` (vérifié le 2026-07-19) — c'est un emplacement réservé, pas une implémentation.

## Implémenté

Aucun — planifié.

## Vérifié

Aucun.

## Dette connue

Sans objet (spec non démarrée).

## Dépendances

- `depends_on` : [SPEC-4](spec-4-user-crud-and-scopes.md) (les hooks visés au départ concernent la logique transverse à la création d'utilisateurs, ex. appartenances de groupe par défaut), [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md) — cohérent avec [`../../product/roadmap.md`](../../product/roadmap.md), qui regroupe explicitement Specs 5–8 comme ne devant démarrer qu'après SEC-01
- `blocks` : —

## Critères d'acceptation

À définir.

## Preuves

- `src/CoreApi/Hooks/.gitkeep` (seul contenu du dossier, confirmé 2026-07-19)

## Prochaines étapes

Non planifiée avant [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md) et avant que Specs 5/6 existent (un hook sur la création d'utilisateur peut vouloir manipuler des groupes).
