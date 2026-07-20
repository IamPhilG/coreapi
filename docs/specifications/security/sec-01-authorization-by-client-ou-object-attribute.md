---
id: SEC-01
title: Autorisation par client, cible, OU, objet et attribut
type: security
status: planned
last_reviewed: "2026-07-20"
---

# SEC-01 — Autorisation par client, cible, OU, objet et attribut

## Statut réel

**Planned.** Aujourd'hui, seuls deux mécanismes existent : le scope OAuth (tier/ressource/verbe) et un confinement structurel unique au sous-arbre `BaseDn` configuré. Aucune autorisation différenciée par client, par OU individuelle, par objet, ou par attribut n'existe.

## Implémenté

- Scope exact par tier/ressource/verbe (`coreapi.ad.t2.users.*`) — [SPEC-4](../functional/spec-4-user-crud-and-scopes.md).
- Confinement structurel unique au `BaseDn` configuré — `EnsureWithinConfiguredBaseDn`, [SPEC-4](../functional/spec-4-user-crud-and-scopes.md).

## Vérifié

Voir [SPEC-4](../functional/spec-4-user-crud-and-scopes.md) pour ce qui est déjà couvert (confinement `BaseDn`). Rien au-delà.

## Dette connue

- Un porteur du scope `users.update` peut modifier tous les champs exposés (`Department`, `Manager`, etc.) sans granularité par attribut.
- Aucune différenciation par OU individuelle (un seul périmètre global).
- Aucune notion de "client" comme axe d'autorisation distinct du scope porté par son token.

## Dépendances

- `depends_on` : [SPEC-4](../functional/spec-4-user-crud-and-scopes.md)
- `blocks` : [SPEC-5](../functional/spec-5-service-accounts.md), [SPEC-6](../functional/spec-6-groups-and-ous.md), [SPEC-7](../functional/spec-7-acl-and-delegations.md), [SPEC-8](../functional/spec-8-business-logic-hooks.md)

## Modèle de référence

Le modèle complet (sept briques de classification, taxonomie des scopes, fiche d'intégration client) est porté par [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) — non dupliqué ici. Cette fiche ne définit que les incréments à réaliser pour le faire progresser ; elle ne reformule pas le modèle lui-même.

## Incréments à réaliser

1. **Mécanisme de détermination du tier à l'exécution** (brique 1 du modèle) — un service qui résout le tier d'un objet AD cible selon la cascade actée (Tier 0 ? → Tier 2 ? → Tier 1 ? → défaut Tier 0), au-delà du cas `users`=Tier 2 codé en dur aujourd'hui.
2. **Inventaire des classes d'objet AD** (brique 4) — préalable à toute extension au-delà de Spec 4 (comptes de service, groupes, OU, ACL).
3. **Restriction d'attributs pour `read`** — permettre à un scope `read` de n'exposer qu'un sous-ensemble non sensible d'attributs, au lieu de l'ensemble fixe actuel de `UserDto`.
4. **Portée fine** (brique 6) — dépasser le contrôle binaire `BaseDn` actuel vers une portée par OU spécifique, par objet individuel, ou par attribut.
5. **Champ "plan/tier de l'intermédiaire" dans la fiche d'intégration** (brique 2) — actuellement absent du schéma de fiche proposé.
6. **Décomposition des gestes en droits AD élémentaires** (brique 5) — mapping "geste métier → opérations AD élémentaires", nécessaire avant Spec 7 (ACL).

## Critères d'acceptation

À définir par incrément ci-dessus, au moment de sa mise en œuvre.

## Preuves

Aucune (planifié). Modèle de référence : [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md).

## Prochaines étapes

Bloque toute extension au-delà du Tier 2 (Specs 5–7) — à traiter avant.
