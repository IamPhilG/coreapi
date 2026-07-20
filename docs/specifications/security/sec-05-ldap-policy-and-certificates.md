---
id: SEC-05
title: Politique LDAP et certificats
type: security
status: planned
last_reviewed: "2026-07-20"
---

# SEC-05 — Politique LDAP et certificats

## Statut réel

**Planned** pour la politique de signing/channel binding ; **déjà implémenté et vérifié** pour LDAPS + validation de certificat/hostname (voir [SPEC-2](../enablers/spec-2-ldap-connection-layer.md)). Cet item ne répète pas ce qui est acquis — il capture ce qui reste ouvert.

## Implémenté

Voir [SPEC-2](../enablers/spec-2-ldap-connection-layer.md) : LDAPS obligatoire hors Development, validation de certificat par chaîne de confiance et correspondance exacte de nom d'hôte (pas de wildcard).

## Vérifié

Voir [SPEC-2](../enablers/spec-2-ldap-connection-layer.md).

## Dette connue

- Deux mécanismes de bind distincts à traiter différemment :
  - **Simple Bind sur LDAPS** (mécanisme actuellement utilisé) : TLS obligatoire, certificat validé, **port 389 interdit pour tout bind porteur d'identifiants** — déjà respecté par CoreAPI lui-même, **violé par l'outillage de test** (Spec 0, seeding de démo) — voir [EN-06](../enablers/en-06-test-dc-hardening.md).
  - **Negotiate/SASL** (non utilisé aujourd'hui, chemin de repli non voulu si `ServiceAccountUser` est vide) : si ce mécanisme devait être adopté délibérément, la signature LDAP (`Signing`) deviendrait obligatoire et le channel binding serait à évaluer selon le mécanisme SASL précis retenu (Kerberos vs NTLM) — question sans objet tant que seul le Simple Bind sur LDAPS est utilisé.

## Dépendances

- `depends_on` : [SPEC-2](../enablers/spec-2-ldap-connection-layer.md)
- `blocks` : —

## Incréments à réaliser

1. **Corriger la violation du seeding de démo** (Spec 0) — basculer sur LDAPS exclusivement, voir [EN-06](../enablers/en-06-test-dc-hardening.md).
2. **Décision explicite Negotiate/SASL** — seulement si ce mécanisme est un jour adopté délibérément (aujourd'hui un chemin de repli non voulu, voir [SEC-02](sec-02-execution-identities-secrets-least-privilege.md)) : signature LDAP obligatoire + channel binding à évaluer selon le mécanisme SASL retenu.
3. **Cible secretless** (bascule `AuthType.Basic` → `AuthType.Negotiate`/ticket Kerberos) — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §12 ; dépend d'une cible de déploiement validée.

## Critères d'acceptation

- Aucun bind porteur d'identifiants n'est jamais tenté sur le port 389, nulle part dans le dépôt (CoreAPI et outillage de test inclus).
- Décision explicite consignée si Negotiate/SASL est un jour adopté délibérément, avec l'exigence de signing/channel binding correspondante.

## Preuves

- `src/CoreApi/Infrastructure/LdapDirectoryConnection.cs:44-96` (implémenté côté CoreAPI)
- `tests/CoreApi.IntegrationTests/TestInfrastructure/AdDcProvisionerFixture.cs:899` (violation constatée côté outillage de test)

## Prochaines étapes

Corriger la violation côté outillage de test ([EN-06](../enablers/en-06-test-dc-hardening.md)) ; trancher la question Negotiate/SASL seulement si ce mécanisme est un jour envisagé (voir question ouverte dans la revue de sécurité du 2026-07-19).
