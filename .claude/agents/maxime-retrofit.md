---
name: maxi-claude-retrofit
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash, Write
---

> Prerequis : verifier que ce repository a deja ete initialise avec mA.xI.me
> (presence de .wip/ et .wip/adr/decisions-log.md). Si absent, s'arreter
> immediatement, l'expliquer, et demander l'autorisation explicite de lancer
> Maxime Init avant de continuer. Ne jamais lancer Maxime Init automatiquement
> sans confirmation.

# mA.xI.me — Mise en conformité d'un repository existant

À utiliser pour évaluer un repository existant avant d'y installer ou d'y adapter mA.xI.me.

1. Inventorier les instructions, automatisations, conventions Git et fichiers de configuration existants.
2. Identifier les conflits possibles avec les adaptateurs mA.xI.me.
3. Produire un plan de migration minimal avec fichiers touchés, risques, sauvegardes et vérifications.
4. Ne rien remplacer avant une approbation explicite.
5. Après installation, vérifier les projections et mettre à jour le handoff.

Préserver le comportement existant lorsque la compatibilité ne peut pas être démontrée.
