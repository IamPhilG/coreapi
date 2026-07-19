---
name: maxi-claude-handoff
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash, Write
---

> Prerequis : verifier que ce repository a deja ete initialise avec mA.xI.me
> (presence de .wip/ et .wip/adr/decisions-log.md). Si absent, s'arreter
> immediatement, l'expliquer, et demander l'autorisation explicite de lancer
> Maxime Init avant de continuer. Ne jamais lancer Maxime Init automatiquement
> sans confirmation.

# mA.xI.me — Handoff

À utiliser à la fin d'un bloc de travail, sur une décision structurante ou avant l'arrêt d'une session.

1. Exécuter `git status` ; ne jamais lancer de staging global automatique.
2. Créer, sans écraser, `.wip/memory/YYYYMMDD.session-handoff.md`.
3. Y noter : terminé, en cours, blocages, décisions, fichiers modifiés, contexte critique et prochaine action précise.
4. Ajouter les décisions et impasses utiles à `.wip/adr/decisions-log.md` et `.wip/results/dead-ends.md`. Chaque décision référence son test exécutable.
5. Passer en revue les faits rencontrés pendant la session qui pourraient être
   un savoir durable, transversal et publiable (même critère que la règle 6
   de Maxime KB) — pas seulement de la documentation, aussi des pièges
   d'outils ou des comportements de plateforme découverts. Proposer
   explicitement leur capture via Maxime KB, un candidat à la fois — jamais
   de fiche créée automatiquement.
6. Indiquer l'objectif atteint, partiel ou non et la meilleure reprise.

Le handoff est concis, factuel et actionnable. Il ne doit pas être mis à jour après chaque micro-tâche.
