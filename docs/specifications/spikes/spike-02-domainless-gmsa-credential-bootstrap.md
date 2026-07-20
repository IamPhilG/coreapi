---
id: SPIKE-02
title: Domainless gMSA et credential bootstrap
type: spike
status: planned
last_reviewed: "2026-07-19"
---

# SPIKE-02 — Domainless gMSA et credential bootstrap

## Statut réel

**Planned — investigation non commencée.**

## Implémenté

Sans objet.

## Vérifié

Rien à ce jour.

## Dette connue

Le modèle "secretless" documenté (`.wip/kb/active/security-architecture/zero-standing-access.json`) note déjà que le chemin `credentials-fetcher` domainless-gMSA d'AWS Fargate repose lui-même sur un credential statique de bootstrap dans Secrets Manager — la chaîne n'est donc pas secretless de bout en bout par construction. Ce spike doit vérifier concrètement l'ampleur de cette exception.

## Dépendances

- `depends_on` : —
- `blocks` : [SPIKE-01](spike-01-ecs-fargate-linux-viability.md)

## Questions à trancher par ce spike

- Comment le bootstrap du credential initial est-il réellement sécurisé (rotation, portée, durée de vie) ?
- Le gMSA domainless est-il compatible avec le compte de service unique actuel (`DirectoryConnectionOptions.ServiceAccountUser`), ou implique-t-il un changement de modèle d'identité côté CoreAPI ?

## Critères d'acceptation

Un rapport de spike documentant précisément la chaîne de confiance du bootstrap, avec ses limites, avant que [SPIKE-01](spike-01-ecs-fargate-linux-viability.md) ne puisse conclure.

## Preuves

Aucune — investigation non commencée.

## Prochaines étapes

Bloque [SPIKE-01](spike-01-ecs-fargate-linux-viability.md).
