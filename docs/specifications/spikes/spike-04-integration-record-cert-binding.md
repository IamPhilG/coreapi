---
id: SPIKE-04
title: Liaison cryptographique des fiches d'intégration
type: spike
status: planned
last_reviewed: "2026-07-19"
---

# SPIKE-04 — Liaison cryptographique des fiches d'intégration

## Statut réel

**Planned — investigation non commencée.** Une branche repère, `feature/integration-record-cert-binding`, existe dans le dépôt et est décrite dans [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §10 comme "créée le 2026-07-18 pour la version à liaison par certificat [des deux jetons d'intégration], à reprendre si le hash s'avère insuffisant en pratique". **Elle ne contient aucun travail réel.**

## Preuve que la branche est vide

- Base de fusion avec `main` : commit `052713c` (antérieur aux PR #4/#5).
- `git diff --stat main feature/integration-record-cert-binding` : 32 fichiers changés, **uniquement des suppressions** (-2218/+279 lignes) — la branche est strictement en retard sur `main`, pas en avance.
- **Ne jamais présenter cette branche comme du travail en cours ou réalisé.**

## Implémenté

Aucun. Le mécanisme actuellement documenté pour la création/modification d'une fiche d'intégration est un contrôle à deux jetons avec liaison par **hash** (V1) — la liaison par certificat est une extension future envisagée, pas construite.

## Vérifié

Aucun.

## Dette connue

Sans objet — investigation non commencée, à mener seulement si le mécanisme hash (V1) s'avère insuffisant en pratique.

## Dépendances

- `depends_on` : —
- `blocks` : [SEC-02](../security/sec-02-execution-identities-secrets-least-privilege.md) (la stratégie de credential/identité du claim de confiance client dépend de la conclusion de ce spike si elle est un jour menée)

## Questions à trancher par ce spike (si mené)

- Le mécanisme hash (V1) est-il réellement insuffisant en pratique, ou est-ce une anticipation non encore justifiée par un incident/besoin réel ?
- Quel serait le modèle de gestion de cycle de vie du certificat si la liaison par certificat était retenue ?

## Critères d'acceptation

Ne pas démarrer d'implémentation sans qu'un besoin concret ait d'abord été documenté justifiant que le hash (V1) est insuffisant.

## Preuves

- `git diff --stat main feature/integration-record-cert-binding` (constat de vacuité, exécuté le 2026-07-19)
- [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §10 (mécanisme hash V1 actuellement conçu, liaison certificat en extension future ; source historique : `.wip/docs/architecture/authorization-and-access-model.md:197-210`)

## Prochaines étapes

Rester au statut `planned` tant qu'aucun besoin concret ne justifie de le prioriser.
