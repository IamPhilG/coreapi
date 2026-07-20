---
id: EN-01
title: Baseline documentaire et catalogue
type: enabler
status: done
last_reviewed: "2026-07-20"
---

# EN-01 — Baseline documentaire, architecture et catalogue des spécifications

## Statut réel

**Done.** Validé explicitement par Philippe le 2026-07-20 — voir [`../../adr/decisions-log.md`](../../adr/decisions-log.md), entrée datée du 2026-07-20. Cette validation porte sur le contenu produit sous `docs/` en tant que documentation canonique de **CoreAPI uniquement** ; elle ne constitue pas une validation de la gouvernance ou de la roadmap globale d'`IamPhilG`/`OurITRes` (voir périmètre explicite dans [`../../README.md`](../../README.md)).

Réalisé en trois passes : (1) création de l'arborescence `docs/`, réconciliation des Specs 0–10, catalogue, promotion corrigée de la revue de sécurité ; (2) suite à relecture de Philippe, correction de `docs/adr/decisions-log.md` (retrait d'une fausse affirmation d'approbation), migration substantielle des informations uniques identifiées dans `.wip/docs/` vers leurs destinations canoniques sous `docs/`, et création de `docs/architecture/views/authorization-model.md` consolidant les deux documents de conception d'architecture d'autorisation ; (3) seconde relecture (statut candidate explicite, périmètre CoreAPI-only, retrait des références normatives restantes à `.wip/docs/`, cohérence SEC-01/SPEC-8) puis validation explicite finale.

## Implémenté

- Arborescence `docs/` complète (voir [`../../README.md`](../../README.md)).
- Réconciliation des statuts Specs 0–10 avec le code réel (aucune spec présentée comme plus avancée qu'elle ne l'est).
- Catalogue `docs/specifications/catalog.yml` avec les 30 entrées (Specs 0–10, EN-01–10, SEC-01–05, SPIKE-01–04).
- Promotion corrigée de la revue de sécurité du 2026-07-19 vers `docs/assurance/reviews/`.
- `docs/architecture/views/authorization-model.md` : consolidation sans perte de `.wip/docs/architecture/{ad-ds-governance-model.md, authorization-and-access-model.md}`.
- Migration des informations uniques identifiées dans la matrice de relecture vers `docs/adr/decisions-log.md`, `docs/specifications/{functional,enablers}/spec-{0,3,5,6,7,9,10}-*.md`, `docs/implementation/implementation-guide.md`, et `docs/specifications/security/sec-{01..05}-*.md` (référencement du modèle consolidé plutôt que duplication).

## Vérifié

Vérification par lecture croisée : chaque statut du catalogue correspond à une preuve citée (test, commit, ou absence confirmée par recherche). Vérification des liens internes effectuée après chaque passe d'écriture.

## Dette connue

- `docs/assurance/verification-matrix.md` et `docs/assurance/threat-model.md` restent des extraits reformulés de la revue de sécurité, pas des documents indépendamment ré-audités — à enrichir au fil des incréments suivants ([EN-02](en-02-git-hygiene-decisions-traceability.md) et suivants).
- `.wip/docs/*` (6 fichiers) reste en place, non modifié, non déplacé, non marqué `superseded` — la requalification de ces fichiers est explicitement différée à un incrément ultérieur, par décision de Philippe (confirmée à nouveau lors de la validation du 2026-07-20 : « Ne supprime et ne déplace encore aucun ancien fichier sous `.wip/docs/` »).

## Dépendances

- `depends_on` : —
- `blocks` : —

## Critères d'acceptation

- **Validation explicite de Philippe obligatoire avant tout passage à `done`** — satisfait le 2026-07-20 (voir [`../../adr/decisions-log.md`](../../adr/decisions-log.md)). La validation porte sur le contenu produit lui-même, pas seulement sur le plan initial, et sa portée est explicitement limitée à CoreAPI.
- Specs 0–10 conservées, statut aligné sur le code. **Satisfait.**
- Aucune fonctionnalité future déclarée implémentée. **Satisfait.**
- Les deux branches repères vides ne sont pas présentées comme du travail réalisé. **Satisfait.**
- `.wip/` distingué de la documentation canonique, `.wip/` non modifié. **Satisfait.**
- Liens internes valides. **Satisfait** (vérifié à chaque passe).
- Cible ECS Fargate maintenue `candidate`. **Satisfait.**
- CoreAPI documenté comme resource server pur. **Satisfait.**
- Aucun fichier de code modifié. **Satisfait.**
- Aucune instruction spécifique à un outil agentique (Claude Code, commandes de skills, bootstrap de session) migrée dans la documentation produit. **Satisfait.**

## Preuves

- Ce dossier `docs/` lui-même, créé le 2026-07-19, corrigé les 2026-07-20.
- `docs/adr/decisions-log.md`, entrée du 2026-07-20 : validation explicite de Philippe, portée CoreAPI uniquement.
- `git status`/`git diff --stat` vérifiés à chaque passe : aucune modification hors `docs/`.

## Prochaines étapes

[EN-02](en-02-git-hygiene-decisions-traceability.md) (hygiène Git, décisions, traçabilité au fil de l'eau). La requalification de `.wip/docs/*` (ex. `superseded`) reste un incrément séparé, non commencé.
