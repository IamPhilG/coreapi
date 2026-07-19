---
name: maxi-claude-start
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash
---

> Prerequis : verifier que ce repository a deja ete initialise avec mA.xI.me
> (presence de .wip/ et .wip/adr/decisions-log.md). Si absent, s'arreter
> immediatement, l'expliquer, et demander l'autorisation explicite de lancer
> Maxime Init avant de continuer. Ne jamais lancer Maxime Init automatiquement
> sans confirmation.

# mA.xI.me — Démarrage de session

À utiliser lorsqu'une demande devient une tâche de travail ou de modification.

1. Lire le handoff le plus récent dans `.wip/memory/`, s'il existe.
2. Exécuter `git status` et `git log --oneline -10`.
3. Lire le marqueur de version local (`.claude/MAXIME_VERSION` ou
   équivalent par hôte). Si la lecture réseau est autorisée
   (`.wip/tools/kb-network-policy.json`, `network_read`), comparer au SHA
   distant du repo source mA.xI.me (ex. `git ls-remote`, sans cloner ni
   écrire). En cas d'écart, le signaler dans l'évaluation pré-session et
   demander l'autorisation de lancer `maxime-init` pour mettre à jour —
   jamais automatique. Si le réseau est indisponible ou interdit, signaler
   "impossible de vérifier" sans bloquer la suite.
4. Résumer l'état connu en cinq points maximum.
5. Demander si l'objectif est de continuer ou de changer de direction.
6. Formuler cet objectif explicitement dans la sortie de `maxime-start` :
   c'est ce que l'orchestrateur transmet à `maxime-kb` avant de poursuivre
   (voir orchestrateur). `maxime-start` n'a pas d'accès en écriture ni au
   Task/agent tool — il ne déclenche pas `maxime-kb` lui-même, il produit
   l'information dont l'orchestrateur a besoin pour le faire.
7. Produire une évaluation pré-session : état réel, recommandation, risques
   et taille S/M/L/XL.
8. Pour une tâche précise, passer à `maxime-plan` avant toute écriture.

Ne pas modifier de fichier avant l'approbation explicite de la spécification quand la tâche est significative.
