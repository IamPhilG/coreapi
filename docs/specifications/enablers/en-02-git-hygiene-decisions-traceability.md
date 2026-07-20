---
id: EN-02
title: Hygiène Git, décisions et traçabilité
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-02 — Hygiène Git, décisions et traçabilité

## Statut réel

**Planned.** Aucune implémentation — cet item capture un constat de la revue de sécurité du 2026-07-19, pas encore traité.

## Implémenté

Aucun — planifié.

## Vérifié

Aucun.

## Dette connue

- `.git/info/exclude` n'exclut que `.wip.zip`, pas `.wip/` ni `.bkp/`, contrairement à ce qu'affirme `CLAUDE.md` ("exclu de Git via `.git/info/exclude`") — un `git add -A` non vérifié committerait aujourd'hui ces deux arborescences (constat R12 de la revue de sécurité).
- `CLAUDE.md` pointe vers `.wip/adr/decisions-log.md`, qui n'existe pas — le fichier réellement peuplé est `.claude/memory/decisions-log.md`. Cet incrément (EN-01) introduit `docs/adr/decisions-log.md` comme journal canonique ; la question du chemin annoncé par `CLAUDE.md` reste à trancher (voir [`../../adr/decisions-log.md`](../../adr/decisions-log.md)).

## Dépendances

- `depends_on` : [EN-01](en-01-documentation-baseline.md)
- `blocks` : —

## Critères d'acceptation

- `.git/info/exclude` couvre réellement `.wip/` et `.bkp/` (jamais via un `.gitignore` versionné, conformément à la règle du projet).
- `CLAUDE.md` et l'emplacement réel du journal de décisions sont cohérents.

## Preuves

Aucune (planifié).

## Prochaines étapes

Corriger `.git/info/exclude` et aligner `CLAUDE.md` sur l'emplacement réel du journal de décisions.
