---
id: SPEC-1
title: Squelette projet
type: enabler
status: done
last_reviewed: "2026-07-19"
---

# Spec 1 — Squelette projet

## Statut réel

**Done.** Fusionné à l'origine de l'historique `main` (branche `feature/spec-1`, ancêtre direct). Aucun document de spec dédié n'a été trouvé sous `.wip/docs/specs/` — la trace de cette spec est l'historique Git lui-même.

## Implémenté

- Structure de solution `coreapi.sln`, projets `src/CoreApi`, `tests/CoreApi.UnitTests`, `tests/CoreApi.IntegrationTests`, `tools/DevTokenMinter`.
- `.editorconfig`/conventions, `TreatWarningsAsErrors=true`, `RestorePackagesWithLockFile=true` (restauration déterministe via `packages.lock.json`) sur tous les projets.
- Dossiers `Controllers/`, `Services/`, `Infrastructure/`, `Models/`, `Hooks/` conformes à la convention documentée dans le `README.md` racine.

## Vérifié

Fusionné et stable depuis l'origine ; aucun test dédié au scaffold lui-même n'est attendu (il n'y a rien à tester dans un squelette de projet).

## Dette connue

Aucune connue.

## Dépendances

- `depends_on` : —
- `blocks` : [SPEC-2](spec-2-ldap-connection-layer.md)

## Critères d'acceptation

`dotnet build` réussit sur la solution complète (déjà atteint).

## Preuves

- Historique Git, branche `feature/spec-1`, ancêtre de `main`.

## Prochaines étapes

Aucune — spec close.
