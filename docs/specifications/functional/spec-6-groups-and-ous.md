---
id: SPEC-6
title: Groupes et OU
type: functional
status: not-started
last_reviewed: "2026-07-20"
---

# Spec 6 — Groupes et OU

## Statut réel

**Not-started.** Aucun contrôleur, service ou test correspondant n'existe (recherche exhaustive effectuée le 2026-07-19). Ne pas présenter cette spec comme implémentée.

## Implémenté

Aucun — planifié.

## Vérifié

Aucun.

## Dette connue

Sans objet (spec non démarrée).

## Dépendances

- `depends_on` : [SPEC-4](spec-4-user-crud-and-scopes.md), [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md)
- `blocks` : [SPEC-7](spec-7-acl-and-delegations.md) (les délégations s'appuient typiquement sur des groupes/OU existants)

## Critères d'acceptation

À définir. Devra clarifier si les OU sont gérées comme un objet de premier ordre ou seulement comme un paramètre de confinement (cas actuel de [SPEC-4](spec-4-user-crud-and-scopes.md)).

## Conception prévue (non implémentée — migrée depuis `.wip/docs/coreapi.md`)

- Le champ `groupType` devra être documenté avec toutes les combinaisons valides de portée × type de groupe (sécurité/distribution × domaine local/global/universel).
- La suppression d'une OU protégée (`force=false`, comportement AD par défaut) doit échouer si l'OU porte la protection contre suppression accidentelle — les appelants devront explicitement passer `force=true` pour retirer d'abord l'ACE DENY de protection avant suppression.
- Contribution Swagger prévue : documentation XML, `[ProducesResponseType]`, tags `[ApiExplorerSettings(GroupName = "Groups")]` et `[ApiExplorerSettings(GroupName = "OUs")]` séparés.
- Sensibilité de l'objet cible non tranchée : un groupe de distribution ordinaire et un groupe de sécurité privilégié ne devraient probablement pas être traités identiquement même s'ils sont tous deux "des groupes" — voir la question ouverte correspondante dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §16.

## Preuves

Aucune.

## Prochaines étapes

Ne pas démarrer avant [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md).
