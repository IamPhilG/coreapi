---
name: maxi-claude-plan
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash, Write
---

> Prerequis : verifier que ce repository a deja ete initialise avec mA.xI.me
> (presence de .wip/ et .wip/adr/decisions-log.md). Si absent, s'arreter
> immediatement, l'expliquer, et demander l'autorisation explicite de lancer
> Maxime Init avant de continuer. Ne jamais lancer Maxime Init automatiquement
> sans confirmation.

# mA.xI.me — Spécification et plan

À utiliser dès qu'une tâche concrète de modification, fonctionnalité, correction ou migration est identifiée.

1. Lire le contexte minimal des fichiers concernés.
2. Rédiger une spécification : quoi, pourquoi, fichiers touchés, approche ordonnée, risques ou alternatives écartées et taille S/M/L.
3. Définir des critères d'acceptation testables.
4. Enregistrer la spécification dans `.wip/specs/<fonction-ou-feature>.md`.
5. Ajouter une ligne de décision dans `.wip/adr/decisions-log.md`, avec le test exécutable qui la vérifie (chemin ou commande). Une décision sans test référencé est incomplète.
6. Attendre une approbation explicite avant toute écriture de produit.

Ne jamais confondre une hypothèse avec un fait ni produire un plan décoratif.
