---
id: SPEC-5
title: Comptes de service
type: functional
status: not-started
last_reviewed: "2026-07-20"
---

# Spec 5 — Comptes de service

## Statut réel

**Not-started.** Aucun contrôleur, service ou test correspondant n'existe dans le dépôt (recherche exhaustive effectuée le 2026-07-19 : aucun résultat pour `ServiceAccount*` dans `src/CoreApi` ou `tests/`). Ne pas présenter cette spec comme implémentée sous aucun prétexte.

## Implémenté

Aucun — planifié.

## Vérifié

Aucun — rien à vérifier tant que rien n'existe.

## Dette connue

Sans objet (spec non démarrée).

## Dépendances

- `depends_on` : [SPEC-4](spec-4-user-crud-and-scopes.md) (réutilisation du pipeline JWT/scope/BaseDn), [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md) (l'autorisation fine doit exister avant d'ouvrir une nouvelle ressource sensible)
- `blocks` : —

## Critères d'acceptation

À définir au moment de la spécification détaillée. Devra a minima reprendre le schéma de scope `coreapi.ad.<tier>.serviceaccounts.<verb>` et le confinement `BaseDn` déjà éprouvés par [SPEC-4](spec-4-user-crud-and-scopes.md).

## Conception prévue (non implémentée — migrée depuis `.wip/docs/coreapi.md`)

Éléments de conception déjà couchés avant tout code, à valider/réviser au moment de la spécification détaillée, pas à traiter comme acquis :

- Placement OU, conventions de nommage et jeux d'attributs distincts de ceux des utilisateurs standards (Spec 4) — traité comme une ressource séparée, pas une variante de `users`.
- Le champ `servicePrincipalName` — attribut multi-valeur au format `ServiceClass/FQDN:Port` — devra être documenté explicitement dans le contrat d'API, format que les appelants devront respecter.
- Contribution Swagger prévue (alignée sur la Definition of Done, voir [`../../implementation/implementation-guide.md`](../../implementation/implementation-guide.md)) : documentation XML (`/// <summary>`), `[ProducesResponseType]` pour chaque code retourné, tag `[ApiExplorerSettings(GroupName = "ServiceAccounts")]`.

## Preuves

Aucune.

## Prochaines étapes

Ne pas démarrer avant [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md), conformément à la trajectoire produit.
