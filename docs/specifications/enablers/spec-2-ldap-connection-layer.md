---
id: SPEC-2
title: Connexion LDAP/LDAPS
type: enabler
status: done
last_reviewed: "2026-07-19"
---

# Spec 2 — Connexion LDAP/LDAPS

## Statut réel

**Done**, avec dette technique documentée ci-dessous. Le README racine le note "Done (unit tests pass; integration tests pending real LDAP)" — les tests d'intégration passent bien contre un DC réel depuis la résolution de Spec 0 ; cette mention est donc également à corriger dans le README.

## Implémenté

- `LdapDirectoryConnection` (`src/CoreApi/Infrastructure/LdapDirectoryConnection.cs`) : connexion singleton, bind paresseux protégé par sémaphore, `AutoBind=false`.
- TLS via `SecureSocketLayer`, validation de certificat par chaîne de confiance **et** correspondance exacte du nom d'hôte (pas de wildcard) — `LdapDirectoryConnection.cs:53-96`.
- `LdapFilterEncoder` (RFC 4515) et `LdapDnEncoder` (RFC 4514) — anti-injection LDAP/DN.
- Garde-fou de démarrage : `UseTls` doit être `true` hors `Development`, sinon échec au démarrage (`Program.cs:122-126`).
- Pagination interne (cookie AD, plafond 1000 par page), limite de résultats configurable (`MaxSearchResults`).
- Annulation coopérative via `CancellationToken` lié à un `_shutdownCts`.

## Vérifié

- `tests/CoreApi.UnitTests/Infrastructure/LdapDirectoryConnectionTests.cs`, `LdapDirectoryConnectionCertificateTests.cs`, `LdapFilterEncoderTests.cs`, `LdapDnEncoderTests.cs`, `DirectoryConnectionOptionsTests.cs`.
- `tests/CoreApi.IntegrationTests/Infrastructure/DirectoryConnectionTests.cs` (3 cas contre un DC réel).
- **Non vérifié par test automatisé** : le garde-fou `UseTls` hors Development (implémenté, jamais exercé par un test qui simule un environnement non-Development avec `UseTls=false`).
- **Non vérifié** : une véritable poignée de main TLS échouant fermée sur un certificat invalide (seule la fonction de comparaison de nom d'hôte est testée isolément).

## Dette connue

- Pas de signature/scellement LDAP (`Signing`/`Sealing`) configuré au-delà du TLS de transport — sans objet tant que seul le Simple Bind sur LDAPS est utilisé, pertinent seulement si `Negotiate`/SASL est un jour adopté. Voir [SEC-05](../security/sec-05-ldap-policy-and-certificates.md).
- Identité d'exécution implicite (`AuthType.Negotiate`) non maîtrisée si `ServiceAccountUser` est vide — ce n'est pas un bind anonyme, mais l'identité effective (Kerberos/NTLM du processus) dépend de la topologie de déploiement sans garde-fou explicite. Voir [SEC-02](../security/sec-02-execution-identities-secrets-least-privilege.md).
- Connexion singleton unique, pas de reconnexion automatique après échec transitoire. Voir [EN-10](../enablers/en-10-resilience-readiness-rate-limiting.md).

## Dépendances

- `depends_on` : [SPEC-1](spec-1-project-scaffold.md)
- `blocks` : [SPEC-4](../functional/spec-4-user-crud-and-scopes.md), [SPEC-5](../functional/spec-5-service-accounts.md), [SPEC-6](../functional/spec-6-groups-and-ous.md), [SPEC-7](../functional/spec-7-acl-and-delegations.md)

## Critères d'acceptation

- Connexion LDAPS avec validation de certificat/hostname stricte, refusant un certificat invalide ou un hostname non correspondant (vérifié au niveau unitaire pour la logique de comparaison).
- LDAPS obligatoire hors Development (implémenté, garde-fou non testé automatiquement).

## Preuves

- `tests/CoreApi.UnitTests/Infrastructure/LdapDirectoryConnectionTests.cs`
- `tests/CoreApi.IntegrationTests/Infrastructure/DirectoryConnectionTests.cs`
- `src/CoreApi/Infrastructure/LdapDirectoryConnection.cs:44-96`

## Prochaines étapes

- Ajouter le test manquant du garde-fou `UseTls` hors Development.
- Décision explicite sur le besoin de signing/channel binding si Negotiate/SASL est un jour retenu (SEC-05).
