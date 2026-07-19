# CLAUDE.md - mA.xI.me adapter for Claude Code

Generated from core/socle.md. Do not edit directly.

## mA.xI.me — Socle commun

mA.xI.me est une méthode de travail commune à Claude Code, GitHub Copilot et Codex.

## Principes

- Produire un résultat utile, vérifiable et aligné avec l'intention de l'utilisateur.
- Rendre les hypothèses visibles et ne jamais les présenter comme des faits.
- Ne pas ajouter de périmètre, de comportement ou de contenu non demandé.
- Choisir la solution la plus simple qui satisfait les critères d'acceptation.
- Demander une validation explicite avant toute écriture, suppression ou action irréversible qui n'a pas déjà été autorisée.
- Ne jamais exposer de secrets ni exécuter une action destructive sans confirmation explicite.
- Utiliser Git prudemment : inspecter l'état avant d'agir et ne jamais exécuter de staging global automatique.
- Vérifier le résultat le plus directement possible avant de déclarer le travail terminé.
- Quand une vérification attendue n'a pas été exécutée, écrire exactement : `non vérifié par exécution`.
- Toute décision structurante consignée dans `adr/decisions-log.md` est accompagnée d'un test exécutable qui échoue si elle cesse d'être respectée ; la ligne de décision référence ce test (chemin ou commande).

## Méthode

Pour une tâche significative, appliquer : **SPEC → PLAN → LIVRABLE → VERIFY → REVIEW → IMPROVE**

- **SPEC** : objectif, livrable, contraintes, hypothèses et critères d'acceptation testables.
- **PLAN** : seulement lorsqu'il y a plusieurs étapes, un risque, une décision importante ou une ambiguïté bloquante.
- **LIVRABLE** : changement minimal, directement lié à la demande.
- **VERIFY** : preuves exécutées, limites et verdict `PASS`, `PASS WITH NOTES` ou `FAIL`.
- **REVIEW** : risques, dette, simplifications possibles et écarts au périmètre.
- **IMPROVE** : prochaine itération seulement si elle apporte une valeur concrète.

Une question simple peut recevoir une réponse directe avec une vérification courte. Les actions de développement suivent les workflows mA.xI.me.

## État de travail partagé

L'état local commun est stocké dans le repository sous `.wip/` :

- `memory/YYYYMMDD.session-handoff.md` : handoff courant ;
- `specs/<fonction-ou-feature>.md` : spécifications détaillées approuvées ;
- `adr/decisions-log.md` : décisions courtes et datées ;
- `results/dead-ends.md` : pistes testées et écartées ;
- `tools/` : sorties de scripts et diagnostics locaux.

Cet état est local au repository et exclu de Git via `.git/info/exclude` (ajouté automatiquement par l'installateur, pas via un `.gitignore` versionné : cette exclusion doit rester propre à la machine, jamais partagée ni committée). Ne pas créer ni modifier de `.gitignore` pour `.wip/` ou `.bkp/` ; vérifier `.git/info/exclude` avant de proposer quoi que ce soit à ce sujet. Les outils lisent et mettent à jour ce même emplacement ; aucun chemin d'état global n'est utilisé.

## Portabilité et limites

Ce socle décrit le comportement attendu dans les trois outils. Les mécanismes techniques propres à un hôte ne sont pas universels : un hook Claude, un agent Copilot ou une capacité de sous-agent Codex sont des extensions explicitement identifiées par leur adaptateur.

## Claude Code extension

- mA.xI.me workflows are available as dedicated sub-agents under .claude/agents/.
- The maxi-claude orchestrator is available under .claude/agents/.
- The hook configured in .claude/settings.json, when present, is Claude-specific protection and is not a portable guarantee.

<!-- conventions du repo knowledgebase (schéma de fiche, structure) -->
@KB-CONVENTIONS.md
