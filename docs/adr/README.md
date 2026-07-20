# Décisions (ADR) — CoreAPI

Ce dossier contient le journal des décisions structurantes réellement actées pour CoreAPI, au format déjà utilisé par `.claude/memory/decisions-log.md` (repris ici, migré, canonique désormais).

## Format d'une entrée

```markdown
## AAAA-MM-JJ — Titre court

**Décision** : ce qui a été décidé.

**Approche retenue** : comment, concrètement.

**Alternatives écartées** : quoi, et pourquoi.

**Approuvé par** : qui — date.
```

## Règle stricte

Une décision n'entre dans [`decisions-log.md`](decisions-log.md) que si elle a réellement été prise et approuvée. Une intention, une préférence exprimée en conversation, ou une orientation documentée dans un document de conception (`.wip/docs/architecture/*.md`) **n'est pas une décision actée** tant qu'elle n'est pas consignée ici avec un approbateur et une date. En cas de doute, ne pas ajouter l'entrée — consigner la question comme ouverte dans la spec concernée à la place.

## Pourquoi ce dossier plutôt que `.wip/adr/`

`CLAUDE.md` (racine) documente le journal de décisions comme vivant sous `.wip/adr/decisions-log.md` — ce chemin **n'existe pas** dans le dépôt (vérifié le 2026-07-19). Le fichier réellement peuplé avant cet incrément était `.claude/memory/decisions-log.md`. `docs/adr/decisions-log.md` devient le journal canonique et versionné à partir de cet incrément ; l'écart entre `CLAUDE.md` et l'emplacement réel reste à corriger dans `CLAUDE.md` lui-même (voir [`../specifications/enablers/en-02-git-hygiene-decisions-traceability.md`](../specifications/enablers/en-02-git-hygiene-decisions-traceability.md) — hors périmètre de cet incrément, qui ne modifie aucun fichier hors `docs/`).
