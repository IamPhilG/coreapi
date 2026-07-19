---
name: maxi-claude-init
description: mA.xI.me workflow generated from the canonical source.
tools: Read, Glob, Grep, Bash
---

# mA.xI.me — Initialisation d'un repository (Maxime Init)

À utiliser pour préparer un nouveau repository à recevoir mA.xI.me, ou pour
réinstaller/mettre à jour une installation existante (idempotent, comme
`git init`).

1. Vérifier que le dossier cible est un repository Git.
2. Inspecter le contexte et les conventions existantes sans les écraser.
3. Proposer les adaptateurs à installer : Claude, Copilot, Codex ou tous.
4. Présenter les fichiers à créer ou remplacer et les sauvegardes prévues.
5. Attendre la validation avant l'installation.
6. Exécuter les petits scripts spécialisés correspondants
   (`install/lib/init-local-state.*`, puis `install/lib/install-{claude,copilot,codex}.*`
   selon les adaptateurs validés) — jamais de réécriture manuelle des fichiers
   projetés : ces scripts sont la source de vérité testée.
7. Vérifier les fichiers projetés et l'exclusion Git de `.wip/` et `.bkp/`
   (`.git/info/exclude` et `.gitignore`).
8. Si une knowledge base partagée existe pour ce contexte, proposer
   explicitement `git submodule add <url> knowledge-base` — jamais
   automatique, jamais sans connaître l'URL du repo KB à utiliser. Si
   Philippe ne fournit pas d'URL ou décline, ne pas insister : Maxime KB
   fonctionne déjà avec `.wip/kb/` seul.
9. Demander explicitement si cet environnement autorise l'écriture réseau
   (push vers le repo KB partagé) et la lecture réseau (pull/clone), et
   consigner la réponse dans `.wip/tools/kb-network-policy.json`
   (`network_read`, `network_write`). Par défaut si jamais répondu :
   `network_write: false` — ne jamais présumer une écriture autorisée.
   Maxime KB consulte ce fichier avant toute action réseau.
10. Si cet appel est une **mise à jour** d'une installation existante (pas
    une première installation), afficher le SHA source local
    (`.claude/MAXIME_VERSION` ou équivalent par hôte) et le SHA distant
    avant de redemander confirmation — la mise à jour n'est jamais une
    boîte noire. La mise à jour recompose les mêmes petits scripts
    `install/lib/` que l'étape 6, jamais un script séparé.

L'installation est toujours locale au repository cible ; aucun répertoire global
utilisateur n'est utilisé. Aucun autre agent mA.xI.me (`start`, `plan`,
`handoff`, `retrofit`, `review`, `kb`) ne doit travailler si ce repository n'a
pas encore été initialisé : ce sont eux qui redirigent vers Maxime Init, pas
l'inverse — ce workflow ne dépend d'aucun état préexistant.
