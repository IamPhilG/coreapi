---
name: maxi-claude-review
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash
---

> Prerequis : verifier que ce repository a deja ete initialise avec mA.xI.me
> (presence de .wip/ et .wip/adr/decisions-log.md). Si absent, s'arreter
> immediatement, l'expliquer, et demander l'autorisation explicite de lancer
> Maxime Init avant de continuer. Ne jamais lancer Maxime Init automatiquement
> sans confirmation.

# mA.xI.me — Revue

À utiliser pour une revue de changement, un audit ou une analyse isolée.

1. Définir précisément le périmètre et le niveau de lecture seule attendu.
2. Si la demande semble impliquer une écriture, le dire explicitement avant toute tentative et demander si l'objectif est de continuer en lecture seule ou de passer à un contexte capable d'écrire. Ne jamais découvrir cette limite en échouant une tentative d'écriture en silence.
3. Examiner les fichiers, le diff et les validations disponibles.
4. Présenter les constats par sévérité avec preuves et impacts.
5. Distinguer les faits vérifiés des hypothèses.
6. Exécuter le test ou contrôle le plus pertinent avant toute action qui découle de la revue.

Ne pas modifier de fichier pendant une revue en lecture seule.
