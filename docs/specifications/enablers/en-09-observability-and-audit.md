---
id: EN-09
title: Observabilité et audit
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-09 — Observabilité et audit

## Statut réel

**Planned.** État actuel quasi nul : `/health` est un health check générique (`AddHealthChecks()` sans `IHealthCheck` custom, `Program.cs:43,142`) qui ne vérifie pas la connectivité AD réelle ; seuls des logs `ILogger` ponctuels existent (rejets de certificat, avertissements de pagination), sans piste d'audit structurée des décisions d'autorisation.

## Implémenté

Aucun mécanisme d'audit structuré. Logs ponctuels seulement (`LdapDirectoryConnection`).

## Vérifié

Aucun.

## Dette connue

Aucune traçabilité de "qui a eu 403, sur quel scope, quand" — écart identifié par le threat model (STRIDE, catégorie Repudiation) de la revue de sécurité du 2026-07-19.

## Dépendances

- `depends_on` : [SPEC-4](../functional/spec-4-user-crud-and-scopes.md)
- `blocks` : [SEC-03](../security/sec-03-audit-correlation-non-repudiation.md)

## Critères d'acceptation

- Un `IHealthCheck` custom vérifie réellement la connectivité AD (bind/recherche léger), pas seulement la disponibilité du process.
- Les décisions d'autorisation (401/403, scope demandé/présent) sont journalisées de façon structurée et corrélable.

## Preuves

Aucune (planifié).

## Prochaines étapes

Voir [SEC-03](../security/sec-03-audit-correlation-non-repudiation.md) pour le volet corrélation/non-répudiation, qui dépend de cet item.
