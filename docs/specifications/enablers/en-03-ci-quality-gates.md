---
id: EN-03
title: CI et quality gates
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-03 — CI et quality gates

## Statut réel

**Planned.** Aucune pipeline CI/CD n'existe dans ce dépôt (`.github/workflows/` absent, recherche exhaustive, aucun équivalent Azure/GitLab/Jenkins trouvé).

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

Les 99 tests (94 unitaires + 5 intégration) ne sont exécutés que manuellement, sur le poste d'un développeur — aucune garantie qu'ils soient réellement exécutés avant chaque changement.

## Dépendances

- `depends_on` : —
- `blocks` : [EN-05](en-05-dotnet-10-migration.md) (une migration majeure de TFM devrait être validée par une CI, pas seulement manuellement)

## Critères d'acceptation

- Un workflow **déclenché sur chaque Pull Request** exécute `dotnet build` + `dotnet test --filter Category=Unit` uniquement — aucun accès AWS, aucun secret AD.
- Un workflow **séparé**, à déclenchement manuel (`workflow_dispatch`) ou planifié, exécute les 5 tests d'intégration contre une ressource AWS réelle, jamais automatiquement sur push/PR.
- Les secrets AWS/AD nécessaires à ce second workflow transitent par les mécanismes natifs de secrets de la plateforme CI (jamais en clair), idéalement via une fédération d'identité (OIDC).

## Preuves

Aucune (planifié).

## Prochaines étapes

Priorité P0 du backlog de la revue de sécurité — précède logiquement [EN-05](en-05-dotnet-10-migration.md).
