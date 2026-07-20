---
id: EN-08
title: POC de déploiement AWS
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-08 — POC de déploiement AWS

## Statut réel

**Planned.** Aucun POC mené. Dépend de deux investigations non commencées.

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

Un document de conception historique affirmait déjà "ECS Fargate" décidé sans qu'aucun POC technique ne l'ait validé — réconcilié en cible **candidate** dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §12 — voir [SPEC-9](../enablers/spec-9-aws-deployment.md).

## Dépendances

- `depends_on` : [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md), [SPIKE-02](../spikes/spike-02-domainless-gmsa-credential-bootstrap.md)
- `blocks` : —

## Critères d'acceptation

À définir à l'issue des spikes de validation. Devra a minima prouver l'authentification Kerberos/gMSA domainless et la compatibilité LDAPS depuis un conteneur Linux.

## Preuves

Aucune (planifié).

## Prochaines étapes

Ne pas démarrer avant que SPIKE-01/SPIKE-02 aient produit une conclusion.
