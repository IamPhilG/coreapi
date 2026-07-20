---
id: SPIKE-03
title: Multi-instance et suivi partagé du jti
type: spike
status: planned
last_reviewed: "2026-07-19"
---

# SPIKE-03 — Multi-instance et suivi partagé du `jti`

## Statut réel

**Planned — investigation non commencée.** Une branche repère, `feature/multi-instance-support`, existe dans le dépôt et est décrite dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §14 comme "créée le 2026-07-18 pour la version avec un composant de suivi partagé (type Redis/ElastiCache) entre instances, à reprendre quand la charge le justifiera". **Elle ne contient aucun travail réel.**

## Preuve que la branche est vide

- Base de fusion avec `main` : commit `052713c` (antérieur aux PR #4/#5 — c'est-à-dire antérieur au retrofit d'autorisation par scope actuellement fusionné).
- `git diff --stat main feature/multi-instance-support` : 32 fichiers changés, **uniquement des suppressions** (-2218/+279 lignes) — la branche est strictement en retard sur `main`, pas en avance.
- **Ne jamais présenter cette branche comme du travail en cours ou réalisé.**

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

Aucun mécanisme de suivi `jti` (anti-rejeu) n'existe dans le code actuel, à un seul instance ou plusieurs — confirmé absent par recherche exhaustive.

## Dépendances

- `depends_on` : —
- `blocks` : [SEC-04](../security/sec-04-tiers-jit-privileged-paths.md) (le suivi `jti` partagé est un préalable à tout tier privilégié en environnement multi-instance)

## Questions à trancher par ce spike

- Le suivi `jti` est-il nécessaire avant même le multi-instance, pour un tier privilégié en instance unique ?
- Quel mécanisme de stockage partagé (Redis/ElastiCache ou autre) est le plus adapté une fois la charge le justifiant ?

## Critères d'acceptation

Un rapport de spike, pas une implémentation, concluant sur la nécessité et l'approche avant de réactiver cette branche ou d'en créer une nouvelle.

## Preuves

- `git diff --stat main feature/multi-instance-support` (constat de vacuité, exécuté le 2026-07-19)
- [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §14 (source historique : `.wip/docs/architecture/authorization-and-access-model.md:421-430`)

## Prochaines étapes

À reprendre uniquement quand la charge le justifiera, et seulement après [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md)/[SEC-04](../security/sec-04-tiers-jit-privileged-paths.md).
