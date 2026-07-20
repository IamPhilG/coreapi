# Vue de déploiement

## État actuel

**Aucune cible de déploiement de production n'est décidée.** Constat vérifié le 2026-07-19 : le code ne contient aucun `Dockerfile`, aucune pipeline CI/CD (`.github/workflows/` absent), et les documents de conception se contredisaient entre eux — un seul document de conception historique affirmait "AWS ECS Fargate" décidé, tandis que le `README.md` racine et les autres documents produit disent "TBD (ECS/EKS/Beanstalk)". Cette contradiction est réconciliée dans [`authorization-model.md`](authorization-model.md) §12 : la cible reste **candidate**, pas décidée.

## Position retenue pour cet incrément

ECS Fargate Linux est traité comme une **cible candidate conditionnelle à un POC de validation technique**, pas comme une décision entérinée — voir [`../../specifications/spikes/spike-01-ecs-fargate-linux-viability.md`](../../specifications/spikes/spike-01-ecs-fargate-linux-viability.md) et [EN-08](../../specifications/enablers/en-08-aws-deployment-poc.md). Les points techniques non vérifiés que ce POC devrait trancher :

- Authentification Kerberos/gMSA domainless via `credentials-fetcher` depuis un conteneur Linux (le code actuel utilise encore `AuthType.Negotiate`/`AuthType.Basic`, sans configuration domainless-gMSA).
- Compatibilité LDAPS depuis un conteneur Fargate Linux.
- Bootstrap du credential initial (voir [SPIKE-02](../../specifications/spikes/spike-02-domainless-gmsa-credential-bootstrap.md)).

## Environnement de test/démo (seul environnement qui existe réellement aujourd'hui)

Un DC AD DS jetable, provisionné à la demande sur AWS EC2 (Windows Server), via SSM Run Command + `Install-ADDSForest`. Détail complet : [Spec 0](../../specifications/enablers/spec-0-test-demo-ad-infrastructure.md). Cet environnement présente des lacunes de durcissement connues (secret en clair dans le contenu de la commande SSM, seeding de démo via Simple Bind non chiffré sur le port 389) — voir [EN-06](../../specifications/enablers/en-06-test-dc-hardening.md).

## Ce document sera mis à jour

... dès qu'une décision réelle est prise (voir [`../../adr/decisions-log.md`](../../adr/decisions-log.md)) et que le POC correspondant a produit une preuve exécutable. Jusque-là, aucune affirmation de cible de déploiement décidée ne doit apparaître ailleurs dans `docs/`.
