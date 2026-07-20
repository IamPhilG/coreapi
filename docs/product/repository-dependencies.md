# Dépendances externes — CoreAPI

Dépendances réelles constatées dans le code et la configuration au 2026-07-19/20.

## Périmètre

Cette page est une **vue locale des dépendances constatées depuis CoreAPI** — elle ne prétend pas être le registre autoritatif du portefeuille `IamPhilG`/`OurITRes`. Cette autorité-là sera portée par `OurITRes/archi-projects`. En particulier, des produits/dépôts du portefeuille qui ne sont pas référencés ci-dessous (par exemple IaRBAC, SITARCA, SAGAPI) ne sont **pas** des dépendances de CoreAPI à ce jour — aucune trace de l'un d'eux n'a été trouvée dans ce dépôt ; leur absence ici n'est pas une affirmation sur leur existence ou leur rôle ailleurs dans le portefeuille, seulement un constat local.

## Dépendances d'exécution (runtime)

Ce dont CoreAPI a besoin pour fonctionner, aujourd'hui.

- **Okta** (Authority OIDC) — configuré via `Jwt:Authority`/`Jwt:Audience`/`Jwt:Issuer` (`src/CoreApi/Infrastructure/JwtOptions.cs`). CoreAPI **valide** les tokens émis par cet Authority ; il n'en émet aucun (resource server pur — statut : **implemented, verified**).
- **Active Directory Domain Services** — un ou plusieurs contrôleurs de domaine (LDAP 389 / LDAPS 636), accès via un compte de service configuré (`DirectoryConnectionOptions`) — statut : **implemented, verified**.
- **AWS** (environnement de test/démo uniquement) — EC2 + SSM Run Command pour provisionner un DC AD DS jetable — voir [`specifications/enablers/spec-0-test-demo-ad-infrastructure.md`](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md). **Pas** une dépendance d'exécution de CoreAPI lui-même : aucun déploiement de production n'existe (voir dépendances futures ci-dessous) — statut : **implemented** (infra de test), **not-started** (exécution CoreAPI elle-même sur AWS).

## Dépendances de développement / outillage

Ce qui sert à construire, documenter ou faire évoluer ce dépôt, sans être chargé par CoreAPI à l'exécution.

- **mA.xI.me** (`.claude/agents/maxime*.md`) — outillage d'agent utilisé pour piloter le travail sur ce dépôt (workflows de spec, revue, handoff). **Pas une dépendance runtime de CoreAPI.**
- **knowledge-base** (`.wip/kb/`, ex-submodule `knowledge-base/`) — base de connaissances locale consultée pendant la conception et la rédaction de la documentation. **Pas une dépendance runtime de CoreAPI.**

## Sources de connaissances

Documents/fiches consultés comme référence, pas intégrés au runtime.

- `.wip/kb/active/security-architecture/*.json` — fiches de référence Enterprise Access Model et Zero Standing Access, citées dans [`../architecture/views/authorization-model.md`](../architecture/views/authorization-model.md).
- `.wip/docs/` (6 fichiers, non modifiés) — source historique de la réconciliation de cette documentation, voir [`../README.md`](../README.md) pour son statut.

## Dépendances futures ou proposées

Rien de ceci n'est implémenté aujourd'hui.

- **Cible de déploiement de production sur AWS** — non décidée. ECS Fargate Linux est une cible **candidate**, conditionnelle à [`specifications/spikes/spike-01-ecs-fargate-linux-viability.md`](../specifications/spikes/spike-01-ecs-fargate-linux-viability.md) — statut : **candidate**.
- **Support simultané de plusieurs IdP de confiance** (Entra ID + Okta) — issue GitHub ouverte [`#2`](https://github.com/IamPhilG/coreapi/issues/2), tracée dans [`specifications/functional/spec-3-jwt-authentication.md`](../specifications/functional/spec-3-jwt-authentication.md) — statut : **planned**.
- **Externalisation du provisionneur de test AD DS** (Spec 0) vers un dépôt séparé — décidée en intention (issue [`#3`](https://github.com/IamPhilG/coreapi/issues/3)), séquencée après [EN-06](../specifications/enablers/en-06-test-dc-hardening.md)/[EN-07](../specifications/enablers/en-07-provisioner-contract-and-externalization.md) — statut : **decided** (intention), **not-started** (implémentation).
- **Fiche d'intégration client** (claim de confiance, liaison par hash/certificat) — modèle décrit dans [`../architecture/views/authorization-model.md`](../architecture/views/authorization-model.md) §9-10, pas implémenté — voir [SEC-02](../specifications/security/sec-02-execution-identities-secrets-least-privilege.md) et [SPIKE-04](../specifications/spikes/spike-04-integration-record-cert-binding.md) — statut : **proposed**.

## Relations de portefeuille encore à décider

- **Repositories consommateurs de CoreAPI** — les API "topiques" (gestion des utilisateurs, des groupes, etc.) sont les appelantes prévues de CoreAPI, mais aucune fiche d'intégration formelle n'existe encore pour un consommateur réel identifié.
- **Priorisation inter-produits et gouvernance de portefeuille** — hors périmètre de ce dépôt ; sera portée par `OurITRes/archi-projects` une fois ce dépôt en place. Aucune relation de dépendance ou de priorité avec d'autres produits du portefeuille n'est affirmée ici, faute de registre autoritatif existant à ce jour.
