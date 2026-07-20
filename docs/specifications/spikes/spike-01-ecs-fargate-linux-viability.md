---
id: SPIKE-01
title: Viabilité ECS Fargate Linux
type: spike
status: planned
last_reviewed: "2026-07-19"
---

# SPIKE-01 — Viabilité ECS Fargate Linux

## Statut réel

**Planned — investigation non commencée.** Un document de conception historique affirmait "ECS Fargate" comme cible décidée (réconcilié en cible **candidate** dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §12), mais aucune preuve technique dans le code ne le soutient — le code actuel utilise encore `AuthType.Negotiate`/`AuthType.Basic`, pas de configuration domainless-gMSA. **Cette spike existe précisément pour trancher cette question par la preuve, pas par affirmation.**

## Implémenté

Sans objet — c'est une investigation, pas une implémentation.

## Vérifié

Rien à ce jour.

## Dette connue

La contradiction documentaire elle-même (voir [SPEC-9](../enablers/spec-9-aws-deployment.md)) est la dette que ce spike doit résoudre.

## Dépendances

- `depends_on` : [SPIKE-02](spike-02-domainless-gmsa-credential-bootstrap.md)
- `blocks` : [SPEC-9](../enablers/spec-9-aws-deployment.md), [EN-08](../enablers/en-08-aws-deployment-poc.md)

## Questions à trancher par ce spike

- Authentification Kerberos/gMSA domainless via `credentials-fetcher` depuis un conteneur Fargate Linux — viable ou non ?
- Compatibilité LDAPS depuis un conteneur Linux vers le DC AD DS cible.
- Le bootstrap du credential initial est-il réellement "secretless" de bout en bout, ou repose-t-il sur un secret statique dans Secrets Manager (question soulevée par `.wip/kb/active/security-architecture/zero-standing-access.json`) ?

## Critères d'acceptation

Un rapport de spike concluant explicitement "viable" ou "non viable", avec preuve reproductible (déploiement de test), avant toute mise à jour de [SPEC-9](../enablers/spec-9-aws-deployment.md) vers un statut décidé.

## Preuves

Aucune — investigation non commencée.

## Prochaines étapes

Ne pas commencer avant [SPIKE-02](spike-02-domainless-gmsa-credential-bootstrap.md). Tant que ce spike n'a pas conclu, **ECS Fargate Linux reste `candidate`, jamais `décidé`**, dans tous les documents.
