---
id: SEC-03
title: Audit, corrélation et non-répudiation
type: security
status: planned
last_reviewed: "2026-07-20"
---

# SEC-03 — Audit, corrélation et non-répudiation

## Statut réel

**Planned.** Aucune piste d'audit structurée des décisions d'autorisation n'existe. Un format de log SIEM cible est déjà décidé en conception — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §14 — mais non implémenté.

## Implémenté

Aucun. Seuls des logs `ILogger` ponctuels existent (rejets de certificat, avertissements de pagination) — voir [EN-09](../enablers/en-09-observability-and-audit.md).

## Vérifié

Aucun.

## Dette connue

Impossible aujourd'hui de répondre à "qui a eu 403, sur quel scope, quand" — identifié par le threat model (STRIDE, Repudiation) de la revue de sécurité du 2026-07-19.

## Dépendances

- `depends_on` : [EN-09](../enablers/en-09-observability-and-audit.md)
- `blocks` : —

## Incréments à réaliser

1. **Socle CIM obligatoire** (`action`, `change_type`, `object` avec PII scoping par `piiDisclosureLevel`, `user`, `result`, `vendor_product`, `dest`, `src`) — expédition systématique vers le SIEM, voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §14 pour le schéma exact.
2. **Extension CoreAPI configurable** (`coreapi_jti`, `coreapi_correlationId`, `coreapi_executionPath`, `coreapi_approvalReference`, `coreapi_scope`) — dépend du socle CIM (item 1) et de la fiche d'intégration (non implémentée, voir SEC-02).
3. **Détection de réutilisation de `jti`** — suivi local d'abord (instance unique), suivi partagé ensuite si le multi-instance devient nécessaire (voir [SPIKE-03](../spikes/spike-03-multi-instance-shared-jti-tracking.md)).
4. **Corrélation d'audit inter-segments** — nécessite un identifiant de corrélation partagé et propagé par les systèmes de gouvernance externes (PAM/IIQ/ServiceNow), non confirmé aujourd'hui — question ouverte, pas un item prêt à démarrer.

## Critères d'acceptation

À définir par incrément ci-dessus, à partir du format de log SIEM déjà décidé dans le modèle de référence.

## Preuves

Aucune (planifié).

## Prochaines étapes

Dépend directement de [EN-09](../enablers/en-09-observability-and-audit.md) (mécanisme d'audit de base) avant de pouvoir spécifier la corrélation/non-répudiation.
