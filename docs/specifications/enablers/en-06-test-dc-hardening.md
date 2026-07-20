---
id: EN-06
title: Hardening du DC de test
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-06 — Hardening du DC de test

## Statut réel

**Planned.** Aucune correction appliquée. Les lacunes ci-dessous sont déjà documentées avec preuve dans [SPEC-0](../enablers/spec-0-test-demo-ad-infrastructure.md) et la revue de sécurité du 2026-07-19.

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue (constats à corriger, pas à re-découvrir)

- Mot de passe Domain Administrator en clair dans le contenu de la commande SSM (`AdDcProvisionerFixture.cs:659,664`).
- Simple Bind LDAP non chiffré sur port 389 lors du seeding de démo (`AdDcProvisionerFixture.cs:899`).
- RDP (3389) ouvert systématiquement ; accumulation possible de règles de Security Group non confirmée comme nettoyée.

## Dépendances

- `depends_on` : [SPEC-0](../enablers/spec-0-test-demo-ad-infrastructure.md)
- `blocks` : [EN-07](en-07-provisioner-contract-and-externalization.md)

## Critères d'acceptation

- Le seeding de démo utilise LDAPS (port 636) exclusivement, plus aucun Simple Bind sur 389.
- Le mot de passe Administrator transite via Secrets Manager/Parameter Store SecureString, jamais interpolé en clair dans une charge de commande.
- RDP retiré au profit de SSM Session Manager (déjà utilisé pour tout le reste), ou justifié explicitement s'il doit être conservé.
- Les règles de Security Group sont auditées/purgées à chaque exécution (une seule règle `/32` active à la fois).

## Preuves

Aucune (planifié). Registre de risques complet : [`../../assurance/reviews/2026-07-19-architecture-security-review.md`](../../assurance/reviews/2026-07-19-architecture-security-review.md) §6 (R2, R2bis, R6).

## Prochaines étapes

Traiter avant [EN-07](en-07-provisioner-contract-and-externalization.md) — ne pas externaliser un outil qui conserve ces lacunes.
