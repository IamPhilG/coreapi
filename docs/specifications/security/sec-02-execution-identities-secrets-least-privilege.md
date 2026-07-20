---
id: SEC-02
title: Identités d'exécution, secrets et moindre privilège
type: security
status: planned
last_reviewed: "2026-07-20"
---

# SEC-02 — Identités d'exécution, secrets et moindre privilège

## Statut réel

**Planned** pour le modèle cible (gMSA, secretless, trois profils d'exécution — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §11-12). **Partiellement implémenté** pour l'existant : un compte de service unique, configuré via `DirectoryConnectionOptions`, sans délégation d'identité de l'appelant.

## Implémenté

- Compte de service configurable (`ServiceAccountUser`/`ServiceAccountPassword`), requis ensemble ou absents ensemble (`Program.cs:114-121`).
- Secrets d'application réels (JWT Authority, mot de passe du compte de service) tenus hors du dépôt (`appsettings.Development.json` gitignoré).

## Vérifié

Validation croisée `ServiceAccountUser`/`ServiceAccountPassword` couverte par `DirectoryConnectionOptionsTests.cs`.

## Dette connue

- Identité d'exécution implicite (`AuthType.Negotiate`) non maîtrisée quand `ServiceAccountUser` est vide — dépend entièrement de la topologie de déploiement, aucun garde-fou explicite. **Ce n'est pas un bind anonyme.**
- L'outillage de test (Spec 0) expose le mot de passe Domain Administrator en clair dans le contenu d'une commande SSM et via un Simple Bind non chiffré — voir [EN-06](../enablers/en-06-test-dc-hardening.md).
- Le modèle cible gMSA/secretless (trois comptes, un par profil d'exécution) n'est pas implémenté — seul un compte de service unique existe.
- La liaison cryptographique des fiches d'intégration client (claim de confiance) reste au stade conception — voir [SPIKE-04](../spikes/spike-04-integration-record-cert-binding.md).

## Dépendances

- `depends_on` : [SPEC-2](../enablers/spec-2-ldap-connection-layer.md)
- `blocks` : [EN-08](../enablers/en-08-aws-deployment-poc.md) (une cible AWS doit avoir une stratégie d'identité gMSA validée avant d'être choisie)

## Incréments à réaliser

1. **Câblage DI des trois profils d'exécution** (`user-identity-operations` existe déjà ; `service-identity-operations` et `control-plane-operations` restent à créer, avec leurs comptes de service AD distincts) — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §11.
2. **Migration vers gMSA** pour les trois comptes, en remplacement du compte de service classique actuel (§12 du modèle).
3. **Garde-fou explicite sur l'identité implicite `Negotiate`** — empêcher/documenter la configuration `ServiceAccountUser` vide hors contexte domain-joined intentionnel (priorité P2 du backlog de sécurité).
4. **Bascule vers `AuthType.Negotiate`/ticket Kerberos** pour les trois profils, cible "secretless processus" — dépend d'une cible de déploiement validée (voir [SPIKE-01](../spikes/spike-01-ecs-fargate-linux-viability.md)/[SPIKE-02](../spikes/spike-02-domainless-gmsa-credential-bootstrap.md)).
5. **Mécanisme "no standing access" pour `control-plane-operations`** — plusieurs pistes non tranchées (§12 du modèle) ; à concevoir avant que Spec 7 ne démarre.

## Critères d'acceptation

À définir par incrément ci-dessus. Modèle de référence complet : [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md).

## Preuves

- `src/CoreApi/Infrastructure/LdapDirectoryConnection.cs:35-41` (chemin Negotiate implicite, constat, pas une preuve d'implémentation cible)
- [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) (modèle de référence)

## Prochaines étapes

Garde-fou explicite sur l'identité implicite Negotiate (priorité P2 du backlog de sécurité).
