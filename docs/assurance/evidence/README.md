# Preuves — convention

Ce dossier ne contient **pas** de copie des logs bruts, sorties de commande complètes, ou captures d'écran — celles-ci vivent dans `.wip/results/` (espace de travail local) si elles existent. `docs/assurance/evidence/` référence les preuves par **pointeur** :

- un SHA de commit,
- un chemin de fichier de test + nom de méthode,
- une commande exacte et son résultat résumé (pas la sortie complète),
- un chemin vers un document `.wip/` source si la preuve détaillée y vit déjà.

## Pourquoi pas de duplication

Dupliquer des logs bruts dans `docs/` les fait rapidement diverger de la réalité (le code évolue, le log ne change pas). Un pointeur vérifiable (commit, chemin de test) reste correct ou devient visiblement obsolète si on le vérifie — un log copié-collé, non.

## Où trouver les preuves aujourd'hui

- Registre de risques et preuves détaillées par risque : [`../reviews/2026-07-19-architecture-security-review.md`](../reviews/2026-07-19-architecture-security-review.md) §6.
- Preuves par spécification : [`../verification-matrix.md`](../verification-matrix.md) et [`../../specifications/catalog.yml`](../../specifications/catalog.yml) (`acceptance_evidence`).
- Historique brut des investigations (sessions précédentes) : `.wip/results/` (non canonique, conservé pour traçabilité).
