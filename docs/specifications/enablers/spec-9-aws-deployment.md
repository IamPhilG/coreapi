---
id: SPEC-9
title: Déploiement AWS
type: enabler
status: not-started
last_reviewed: "2026-07-20"
---

# Spec 9 — Déploiement AWS

## Statut réel

**Not-started.** Aucun `Dockerfile`, aucune pipeline CI/CD, aucune ressource de déploiement (task definition, cluster, etc.) trouvée dans le dépôt (recherche exhaustive effectuée le 2026-07-19). La cible elle-même était contradictoire entre documents historiques (un document de conception affirmait "ECS Fargate" décidé, tandis que `README.md` et les autres documents produit disent tous "TBD (ECS/EKS/Beanstalk)") — cette contradiction est réconciliée dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §12 : la cible reste **candidate**, conditionnelle à [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md).

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

La contradiction documentaire elle-même est une dette : un document de conception affirme une décision qu'aucune preuve technique ne soutient dans le code actuel (le code utilise encore `AuthType.Negotiate`/`AuthType.Basic`, pas de configuration domainless-gMSA). Voir la revue de sécurité du 2026-07-19 (§7, correction #10).

## Dépendances

- `depends_on` : [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md)
- `blocks` : —

## Critères d'acceptation

Non définis tant que la cible n'est pas validée par [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md)/[EN-08](en-08-aws-deployment-poc.md).

## Preuves

Aucune (constat d'absence, pas de preuve positive).

## Point technique connu (migré depuis `.wip/docs/coreapi.md`)

Kerberos sur conteneurs Linux nécessite soit un domain-join du conteneur, soit une configuration par keytab — à finaliser une fois la cible AWS choisie (ECS/EKS/Beanstalk, voir [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md)). Ce point est distinct du chemin domainless-gMSA/`credentials-fetcher` propre à Fargate documenté dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §12 — les deux approches (domain-join classique vs domainless gMSA) ne sont pas nécessairement compatibles entre elles et n'ont pas été comparées.

## Prochaines étapes

**ECS Fargate Linux reste une cible candidate, pas une décision** — voir [`../../architecture/views/deployment-view.md`](../../architecture/views/deployment-view.md). Ne pas commencer d'implémentation avant [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md).
