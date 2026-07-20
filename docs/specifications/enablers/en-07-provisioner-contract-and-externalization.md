---
id: EN-07
title: Contrat et externalisation du provisionneur
type: enabler
status: planned
last_reviewed: "2026-07-19"
---

# EN-07 — Contrat et externalisation du provisionneur

## Statut réel

**Planned.** L'externalisation est décidée en intention (issue GitHub [`#3`](https://github.com/IamPhilG/coreapi/issues/3), ouverte le 2026-07-17 : *"Extraire l'outillage Spec 0 (provisioning DC AD DS) dans son propre repo"*) mais **non implémentée**. Aucun contrat (entrées/sorties, garanties d'idempotence, exigences de sécurité) n'est encore défini.

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

Aucune trace, dans le code ou la documentation, d'un travail de définition de contrat ou d'extraction déjà commencé.

## Dépendances

- `depends_on` : [EN-06](en-06-test-dc-hardening.md)
- `blocks` : —

## Séquencement obligatoire (ne pas inverser)

1. Définir le contrat du provisionneur (entrées/sorties, garanties d'idempotence, exigences de sécurité).
2. Corriger le durcissement ([EN-06](en-06-test-dc-hardening.md)).
3. **Seulement ensuite**, extraire vers un dépôt séparé et réutilisable par d'autres projets.

Extraire avant ces deux étapes propagerait les lacunes de sécurité actuelles (voir [EN-06](en-06-test-dc-hardening.md)) à un outil plus largement consommé.

## Critères d'acceptation

- Un document de contrat existe et est versionné avant toute extraction.
- Le durcissement [EN-06](en-06-test-dc-hardening.md) est traité avant l'extraction.

## Preuves

- Issue GitHub [`#3`](https://github.com/IamPhilG/coreapi/issues/3) (décision d'intention, pas d'implémentation).

## Prochaines étapes

Ne pas commencer avant [EN-06](en-06-test-dc-hardening.md).
