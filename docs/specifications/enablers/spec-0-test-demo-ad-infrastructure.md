---
id: SPEC-0
title: Infrastructure AD de test/démonstration
type: enabler
status: partial
last_reviewed: "2026-07-20"
---

# Spec 0 — Infrastructure AD de test/démonstration

*Réconcilie `.wip/docs/specs/spec-0-developer-tooling.md` (statut `in-progress`, décrit un mécanisme DCPROMO/UserData) avec `.claude/handoff/spec-0-status.md` (statut RESOLVED, 2026-07-16), qui documente le pivot réel vers SSM RunCommand. Le document source `.wip/docs/specs/spec-0-developer-tooling.md` n'a jamais été mis à jour après ce pivot — c'est un écart documentaire corrigé ici.*

## Statut réel

**Partial.** Le provisionnement fonctionne de bout en bout et est utilisé par les 5 tests d'intégration, mais des lacunes de sécurité connues subsistent dans l'outillage lui-même (secret en clair, bind non chiffré en mode démo), et l'externalisation prévue n'a pas eu lieu.

## Implémenté

- Provisionnement EC2 Windows Server + promotion AD DS via **AWS Systems Manager Run Command** (`AWS-RunPowerShellScript`) — **pas** EC2 UserData (abandonné : EC2Launch v2 ne décode pas correctement le base64 UserData). Voir `tests/CoreApi.IntegrationTests/TestInfrastructure/AdDcProvisionerFixture.cs:308`.
- Promotion réelle via `Install-ADDSForest` (module `ADDSDeployment`) — **pas** `dcpromo.exe`. `AdDcProvisionerFixture.cs:635-677`.
- Security Group scoping ports 389/636/3389 au `/32` de l'IP de l'opérateur (pas `0.0.0.0/0`) — `tools/setup-test-dc.ps1:406-412`.
- Mode démo (`-Mode demo`) : `KeepRunning=true`, `SeedDemoData=true` (OUs, utilisateurs, groupes, compte de service de démo).
- Idempotence : réutilisation de `ExistingInstanceId` stocké dans `appsettings.Development.json` gitignoré.

## Vérifié

- `.claude/handoff/spec-0-status.md` (RESOLVED, 2026-07-16) : "Both `DirectoryConnectionTests` integration tests pass against a freshly promoted DC."
- `tests/CoreApi.IntegrationTests/Infrastructure/DirectoryConnectionTests.cs` (3 cas) et `tests/CoreApi.IntegrationTests/Services/UserServiceTests.cs` (2 cas) — 5 tests d'intégration au total, non réexécutés dans cette réconciliation documentaire (aucune ressource AWS provisionnée pour EN-01).

## Dette connue

- Le mot de passe Administrator AD est inscrit en clair dans le contenu de la commande SSM (`AdDcProvisionerFixture.cs:659,664`) — visible dans l'historique des commandes/CloudTrail. Pas de Secrets Manager/Parameter Store. Voir [EN-06](en-06-test-dc-hardening.md).
- Le seeding de démo utilise un Simple Bind LDAP non chiffré sur le port 389 avec les identifiants Domain Administrator (`AdDcProvisionerFixture.cs:899`) — devrait obligatoirement passer par LDAPS. Voir [EN-06](en-06-test-dc-hardening.md) et [SEC-05](../security/sec-05-ldap-policy-and-certificates.md).
- RDP (3389) ouvert systématiquement ; nettoyage des règles SG pour d'anciennes IP non confirmé.
- Externalisation vers un dépôt séparé décidée en intention (issue GitHub [`#3`](https://github.com/IamPhilG/coreapi/issues/3)) mais non implémentée — voir [EN-07](en-07-provisioner-contract-and-externalization.md).

## Sécurité réseau — conception d'origine vs état vérifié

*Migré depuis `.wip/docs/specs/spec-0-developer-tooling.md` (2026-06-15) — signalé ici comme écart potentiel, pas comme fait établi, faute d'une nouvelle vérification du Security Group pendant cet incrément.*

La conception d'origine du 2026-06-15 documentait sept règles entrantes sur le Security Group `coreapi-test-dc`, toutes limitées à l'IP publique de l'opérateur : TCP/UDP 53 (DNS), TCP/UDP 88 (Kerberos), TCP/UDP 389 (LDAP), TCP 636 (LDAPS), TCP 3389 (RDP), TCP 445 (SMB, réplication), UDP 123 (NTP). La revue de sécurité du 2026-07-19 n'a vérifié dans le code actuel (`setup-test-dc.ps1:406-412`) que **389, 636 et 3389**. **Cet écart n'a pas été réexpliqué ni revérifié** — il peut s'agir d'une simplification volontaire (DNS/Kerberos/SMB/NTP ne sont peut-être plus nécessaires depuis l'abandon du mécanisme UserData/DCPROMO d'origine) ou d'une règle non documentée ailleurs. À clarifier lors du prochain travail sur [EN-06](en-06-test-dc-hardening.md), pas supposé dans un sens ou l'autre ici.

## Coûts et politique d'exploitation

*Migré depuis `.wip/docs/specs/spec-0-developer-tooling.md`.*

- **Coût observé à la conception** : instance `t3.medium`, ~$0.075/heure (~$2.16/jour si laissée allumée en continu) ; adresse IP élastique gratuite tant qu'associée à une instance en cours d'exécution ; transfert de données négligeable (même région). Le mode test (par défaut) arrête l'instance après chaque run pour limiter les coûts ; le mode démo la laisse tourner — voir la dette réseau ci-dessus sur la fenêtre d'exposition prolongée.
- **Politique de dérive du mot de passe Administrator** : si le mot de passe configuré dans `appsettings.Development.json` ne correspond plus au compte Administrator réel du DC ("The supplied credential is invalid" alors que le port LDAP répond), la réponse attendue est de **recréer le DC de test**, pas de réinitialiser le mot de passe en place. Spec 0 traite ce DC comme une infrastructure de développement jetable ; une dérive de mot de passe signifie que la configuration locale et l'état du domaine ne correspondent plus. Reconstruire via `tools\setup-test-dc.ps1 -AwsProfile default -Mode test` (chemin de recréation).

## Vérification manuelle (hors suite de tests automatisée)

*Migré depuis `.wip/docs/specs/spec-0-developer-tooling.md`.*

Après promotion, une vérification manuelle par RDP/SSM Session Manager peut confirmer l'état du DC : `Get-Service AD*`, `Get-Service DNS`, `Get-ADDomain`, `repadmin /replsummary`. Utile pour diagnostiquer un échec de promotion sans dépendre uniquement des 5 tests d'intégration automatisés.

## Pistes futures identifiées (non cataloguées, à trancher)

*Migré depuis `.wip/docs/specs/spec-0-developer-tooling.md`, section "Future Enhancements" — ces pistes n'ont pas d'entrée dédiée dans `docs/specifications/catalog.yml` aujourd'hui.*

- **Version Terraform** de ce provisionnement (infrastructure as code, état distant) — recoupe potentiellement [EN-07](en-07-provisioner-contract-and-externalization.md) si l'externalisation retient cette approche, à confirmer plutôt qu'à présupposer.
- **Durcissement CIS** du DC de test (au-delà des correctifs déjà catalogués dans [EN-06](en-06-test-dc-hardening.md)) — restriction de l'accès LDAP anonyme, LDAPS obligatoire de bout en bout.
- **AMI personnalisée** pré-intégrant les rôles DNS + AD DS, pour réduire le temps de premier lancement.
- **Support multi-région** — hors périmètre de tout item actuellement catalogué.

## Dépendances

- `depends_on` : —
- `blocks` : [EN-06](en-06-test-dc-hardening.md), [EN-07](en-07-provisioner-contract-and-externalization.md)

## Critères d'acceptation

- Un DC AD DS est promu et joignable en LDAP depuis une exécution du script, de bout en bout, sans intervention manuelle autre que les réponses au wizard (déjà atteint).
- Les 5 tests d'intégration passent contre le DC provisionné (déjà atteint, par le passé — voir `.claude/handoff/spec-0-status.md`).

## Preuves

- `.claude/handoff/spec-0-status.md`
- `tools/setup-test-dc.ps1`
- `tests/CoreApi.IntegrationTests/TestInfrastructure/AdDcProvisionerFixture.cs`

## Prochaines étapes

[EN-06](en-06-test-dc-hardening.md) (corriger les lacunes de sécurité de l'outillage) **avant** [EN-07](en-07-provisioner-contract-and-externalization.md) (définir le contrat puis externaliser) — l'ordre est important, voir la révision 2 de la revue de sécurité du 2026-07-19.
