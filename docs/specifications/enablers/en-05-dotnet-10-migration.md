---
id: EN-05
title: Migration .NET 10 LTS
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-05 — Migration .NET 10 LTS

## Statut réel

**Planned.** CoreAPI cible `net9.0` partout (`src/CoreApi/CoreApi.csproj` et tous les autres `.csproj`). Aucune migration effectuée.

## Implémenté

Aucun changement de TFM.

## Vérifié

Vérification externe effectuée le 2026-07-19 (sources Microsoft officielles) : .NET 9 (STS) et .NET 8 (LTS) atteignent tous deux leur fin de support le 10 novembre 2026 ; .NET 10 (LTS, sorti le 11 novembre 2025) est supporté jusqu'en novembre 2028.

## Dette connue

Aucune marge de sécurité à rester sur .NET 9 par rapport à un saut direct vers .NET 10, puisque .NET 8 et .NET 9 partagent la même échéance de fin de support.

## Dépendances

- `depends_on` : [EN-03](en-03-ci-quality-gates.md) (une migration de TFM doit être validée par une CI, pas seulement manuellement)
- `blocks` : —

## Deux actions distinctes (à ne pas confondre)

1. **Mise à jour corrective de routine sur .NET 9** : tant qu'il reste supporté (jusqu'au 10 novembre 2026), appliquer les correctifs de service/sécurité au sein de la même TFM — action de routine, à faible risque, peut continuer indépendamment de la migration majeure.
2. **Migration majeure vers .NET 10 LTS** : changement de TFM, revalidation des dépendances (`System.DirectoryServices.Protocols`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Swashbuckle.AspNetCore`), régénération des `packages.lock.json`, passage complet de la suite de tests — projet à part entière, à planifier avant novembre 2026.

## Critères d'acceptation

- `dotnet build` et les 99 tests passent sous `net10.0` sans changement de comportement observable.
- Les 5 tests d'intégration passent contre un DC réel (exécution avec autorisation explicite).
- Aucune régression sur les garde-fous de démarrage (`ValidateOnStart` JWT/LDAP).

## Preuves

Mini-spec complète (options, recommandation, stratégie de retour arrière) : [`../../assurance/reviews/2026-07-19-architecture-security-review.md`](../../assurance/reviews/2026-07-19-architecture-security-review.md) §10.

## Prochaines étapes

Planifier la migration majeure avant le 10 novembre 2026, une fois [EN-03](en-03-ci-quality-gates.md) en place.
