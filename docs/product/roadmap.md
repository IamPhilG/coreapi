# Trajectoire — CoreAPI

Reformulation de la trajectoire déjà établie dans la revue d'architecture/sécurité du 2026-07-19 (§8, révision 2) — voir [`../assurance/reviews/2026-07-19-architecture-security-review.md`](../assurance/reviews/2026-07-19-architecture-security-review.md) pour le détail intégral et les preuves. Ce document ne duplique pas l'analyse, il en présente la synthèse et les liens vers le catalogue.

## Périmètre

Cette trajectoire est celle de **CoreAPI seul** — l'ordre dans lequel ce dépôt traite ses propres fondations, sa démo et sa production. Elle ne priorise pas CoreAPI par rapport aux autres dépôts d'`IamPhilG`/`OurITRes`, et ne préjuge d'aucun calendrier inter-produits : cette gouvernance de portefeuille sera portée par `OurITRes/archi-projects`.

## État présent

Gateway REST Tier 2 (utilisateurs standards) fonctionnelle : CRUD complet, JWT + scopes tiered vérifiés par test, anti-injection LDAP/DN vérifié, confinement au sous-arbre `BaseDn` configuré vérifié. Pas de limitation de débit, Swagger non gardé par environnement, DC de test provisionné mais avec une lacune de gestion des secrets, aucune CI. Détail : [`capability-map.md`](capability-map.md).

## Prochaine fondation obligatoire (avant d'étendre au-delà du Tier 2)

Backlog complet et priorisé : [`../assurance/reviews/2026-07-19-architecture-security-review.md#11-backlog-priorisé`](../assurance/reviews/2026-07-19-architecture-security-review.md). Éléments correspondants dans le catalogue : [EN-03](../specifications/enablers/en-03-ci-quality-gates.md) (CI), [EN-04](../specifications/enablers/en-04-secure-demo.md) (Swagger gardé + démo sécurisée), [EN-06](../specifications/enablers/en-06-test-dc-hardening.md)/[EN-07](../specifications/enablers/en-07-provisioner-contract-and-externalization.md) (durcissement + contrat du provisionneur de test).

## Démo

Nécessite [EN-04](../specifications/enablers/en-04-secure-demo.md) — mode démo Swagger avec profils prédéfinis, sans jamais contourner l'authentification/autorisation réelles, et sans que CoreAPI émette de token (le jeton reste signé par un outil séparé, `DevTokenMinter`).

## Production

Au minimum : tous les enablers P0/P1 du backlog, [EN-06](../specifications/enablers/en-06-test-dc-hardening.md) exécuté, [EN-07](../specifications/enablers/en-07-provisioner-contract-and-externalization.md) séquencé (contrat + durcissement avant externalisation), une décision de version .NET ([EN-05](../specifications/enablers/en-05-dotnet-10-migration.md)), [EN-09](../specifications/enablers/en-09-observability-and-audit.md) (observabilité/audit aujourd'hui absent), et la levée des contradictions documentaires (déjà en cours via cet incrément).

## Au-delà (planifié, pas commencé)

- Autorisation par client/OU/objet/attribut : [SEC-01](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md)
- Identités d'exécution, secrets, moindre privilège : [SEC-02](../specifications/security/sec-02-execution-identities-secrets-least-privilege.md)
- Audit, corrélation, non-répudiation : [SEC-03](../specifications/security/sec-03-audit-correlation-non-repudiation.md)
- Tier 1, Tier 0, JIT, chemins privilégiés : [SEC-04](../specifications/security/sec-04-tiers-jit-privileged-paths.md)
- Comptes de service, groupes/OU, ACL, hooks (Specs 5–8) — aucun ne doit démarrer avant que SEC-01 et les fondations ci-dessus soient traitées.
- Déploiement AWS réel (Spec 9) — conditionné à [SPIKE-01](../specifications/spikes/spike-01-ecs-fargate-linux-viability.md) et [EN-08](../specifications/enablers/en-08-aws-deployment-poc.md).
- Migration .NET 10 LTS ([EN-05](../specifications/enablers/en-05-dotnet-10-migration.md)) — à planifier avant le 10 novembre 2026 (fin de support .NET 9), indépendamment du reste.

## Ce qui n'est pas sur cette trajectoire tant qu'une décision n'a pas été prise

- ECS Fargate Linux reste `candidate`, pas décidé — voir [SPIKE-01](../specifications/spikes/spike-01-ecs-fargate-linux-viability.md).
- Le multi-instance ([SPIKE-03](../specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md)) et la liaison par certificat des fiches d'intégration ([SPIKE-04](../specifications/spikes/spike-04-integration-record-cert-binding.md)) ne sont que des marqueurs — les branches associées ne contiennent aucun travail réel.
