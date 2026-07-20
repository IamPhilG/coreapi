---
id: SEC-04
title: Tier 1, Tier 0, JIT et chemins privilégiés
type: security
status: planned
last_reviewed: "2026-07-20"
---

# SEC-04 — Tier 1, Tier 0, JIT et chemins privilégiés

## Statut réel

**Planned.** Seul le Tier 2 (utilisateurs standards) est implémenté. Le JIT Control Plane, les tiers 0/1, le modèle multi-segment PAM, et le suivi partagé de `jti` sont documentés au niveau conception — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §4, §7, §12 — mais absents du code — confirmé (aucune classe/fichier correspondant sous `src/CoreApi/`).

## Implémenté

Aucun.

## Vérifié

Aucun.

## Dette connue

Le document de conception lui-même note que le mécanisme "sans accès permanent" pour `control-plane-operations` reste non conçu ("Ouvert, non résolu").

## Dépendances

- `depends_on` : [SEC-01](sec-01-authorization-by-client-ou-object-attribute.md)
- `blocks` : [SPEC-7](../functional/spec-7-acl-and-delegations.md)

## Incréments à réaliser

1. **Mécanisme de vérification proportionnel par tier** — Tier 2 (claim JWT, déjà implémenté), Tier 1 (non tranché — claim renforcé et/ou API-à-API selon la sensibilité, à trancher avant que Spec 5 touche un cas réel de Tier 1), Tier 0/Control Plane (vérification API-à-API en direct, non implémentée) — voir [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md) §7.
2. **Mécanisme "no standing access" pour `control-plane-operations`** — à concevoir (§12 du modèle ; l'ancienne hypothèse d'une rotation JIT du mot de passe gMSA par un coffre-fort externe est techniquement infondée et écartée).
3. **Couche réseau combinée Control Plane** (segmentation réseau + mTLS par plan) — §13 du modèle, non implémentée.
4. **Vérification API-à-API vers un système PAM/gouvernance** — protocole non défini, pas urgent (aucun code n'atteint ce chemin aujourd'hui).
5. **Suivi partagé du `jti` en environnement multi-instance** — dépend de [SPIKE-03](../spikes/spike-03-multi-instance-shared-jti-tracking.md) (spike non commencé, branche marqueur vide).

## Critères d'acceptation

À définir par incrément ci-dessus.

## Preuves

Aucune (planifié). Modèle de référence : [`../../architecture/views/authorization-model.md`](../../architecture/views/authorization-model.md).

## Prochaines étapes

Ne pas démarrer avant [SEC-01](sec-01-authorization-by-client-ou-object-attribute.md). Bloque explicitement [SPEC-7](../functional/spec-7-acl-and-delegations.md) (les délégations touchent directement au pouvoir maximal AD).
