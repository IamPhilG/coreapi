---
id: SPEC-7
title: ACL et délégations
type: functional
status: not-started
last_reviewed: "2026-07-20"
---

# Spec 7 — ACL et délégations

## Statut réel

**Not-started.** Aucun contrôleur, service ou test correspondant n'existe (recherche exhaustive effectuée le 2026-07-19). Ne pas présenter cette spec comme implémentée.

## Implémenté

Aucun — planifié.

## Vérifié

Aucun.

## Dette connue

Sans objet (spec non démarrée). C'est probablement la spec fonctionnelle la plus sensible du catalogue — elle touche directement au modèle de pouvoir maximal AD (délégation = capacité d'accorder des droits), donc elle ne devrait démarrer qu'après que [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md) et [SEC-04](../security/sec-04-tiers-jit-privileged-paths.md) aient une réponse au moins partielle.

## Dépendances

- `depends_on` : [SPEC-6](spec-6-groups-and-ous.md), [SEC-01](../security/sec-01-authorization-by-client-ou-object-attribute.md), [SEC-04](../security/sec-04-tiers-jit-privileged-paths.md)
- `blocks` : —

## Critères d'acceptation

À définir.

## Conception prévue (non implémentée — migrée depuis `.wip/docs/coreapi.md`)

- Lecture/écriture de DACL sur des objets AD, applicable à tout type d'objet (utilisateurs, comptes de service, groupes, OU) via `System.Security.AccessControl`.
- Les résultats de lecture/écriture d'ACL devront être recoupés avec la sortie PowerShell `Get-Acl`/`Set-Acl` sur le même objet (critère de qualité "Correctness", voir [`../../implementation/implementation-guide.md`](../../implementation/implementation-guide.md)).
- Contribution Swagger prévue : documentation XML, `[ProducesResponseType]`, tag `[ApiExplorerSettings(GroupName = "ACL")]`, documentation du modèle d'ACE en `<remarks>` (valeurs de l'énumération des droits, signification des GUID `ObjectType`, drapeaux d'héritage), au moins un exemple de requête montrant l'octroi d'un droit étendu `Reset Password`.
- C'est la spec fonctionnelle la plus sensible du catalogue au sens du modèle d'autorisation — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) (brique 5 « geste et droit élémentaire », profil d'exécution `control-plane-operations`).

## Preuves

Aucune.

## Prochaines étapes

Ne pas démarrer avant SEC-01 et SEC-04.
