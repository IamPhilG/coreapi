# CoreAPI — Documentation

## Statut de cette documentation

**`docs/` est la documentation canonique de CoreAPI**, validée explicitement par Philippe le 2026-07-20 (voir [`adr/decisions-log.md`](adr/decisions-log.md), incrément [EN-01](specifications/enablers/en-01-documentation-baseline.md), statut `done`).

- **Portée de cette validation : CoreAPI uniquement.** Elle ne constitue pas une validation de la gouvernance ou de la roadmap globale des organisations `IamPhilG`/`OurITRes` — voir « Périmètre » ci-dessous.
- [`.wip/docs/`](../.wip/docs/) reste une **source historique de vérification** : c'est là que vivent les documents de conception d'origine à partir desquels `docs/` a été réconcilié. Ces fichiers ne sont ni déplacés ni supprimés à ce stade, et ne sont pas encore requalifiés (par exemple `superseded`) — cette requalification, si elle a lieu, sera un incrément séparé.
- En cas de divergence entre `.wip/` et `docs/`, **`docs/` fait foi**.

Ce répertoire est versionné, revu, et destiné à être lu par toute personne qui n'a pas suivi les conversations de travail au jour le jour.

`docs/` ne contient jamais de brouillon, d'analyse intermédiaire, de handoff de session ou de log brut — ce contenu vit sous [`.wip/`](../.wip/) (voir « Où sont les brouillons ? » ci-dessous) et n'est promu ici qu'après validation explicite.

## Périmètre

Les documents sous `docs/` couvrent **uniquement le produit et le dépôt CoreAPI** — architecture technique, spécifications, sécurité, trajectoire de ce service précis. Ils ne constituent **ni la gouvernance ni la roadmap globale** de `IamPhilG` ou d'`OurITRes` : la priorisation inter-produits et les relations de portefeuille entre dépôts ne sont pas décidées ici. La future source de vérité du portefeuille global sera portée par le dépôt `OurITRes/archi-projects` (pas encore intégré à cette documentation — aucun lien n'est créé vers ce dépôt tant que son contenu n'existe pas).

## Où trouver quoi

| Je cherche... | Aller à |
|---|---|
| Ce que fait CoreAPI et pourquoi | [`product/vision.md`](product/vision.md) |
| Quelles capacités existent, lesquelles sont prévues | [`product/capability-map.md`](product/capability-map.md) |
| Où va le produit (fondation → démo → production) | [`product/roadmap.md`](product/roadmap.md) |
| Dépendances externes (IdP, AWS, etc.) | [`product/repository-dependencies.md`](product/repository-dependencies.md) |
| L'architecture technique complète | [`architecture/coreapi-sad.md`](architecture/coreapi-sad.md) |
| Les diagrammes (contexte, flux d'autorisation, déploiement) | [`architecture/views/`](architecture/views/) |
| Comment construire, tester, lancer le projet | [`implementation/implementation-guide.md`](implementation/implementation-guide.md) |
| Le catalogue de toutes les spécifications et leur statut réel | [`specifications/README.md`](specifications/README.md) + [`specifications/catalog.yml`](specifications/catalog.yml) |
| Les décisions actées et leurs alternatives écartées | [`adr/decisions-log.md`](adr/decisions-log.md) |
| Le modèle de menaces | [`assurance/threat-model.md`](assurance/threat-model.md) |
| Ce qui est vérifié par test vs simplement affirmé | [`assurance/verification-matrix.md`](assurance/verification-matrix.md) |
| Les revues d'architecture/sécurité passées | [`assurance/reviews/`](assurance/reviews/) |
| Comment exploiter CoreAPI (runbooks) | [`operations/README.md`](operations/README.md) |

## Où sont les brouillons ?

`.wip/` (à la racine du dépôt) reste l'espace de travail local : brouillons, analyses en cours, handoffs de session, résultats bruts, logs temporaires. Il n'est ni supprimé ni remplacé par cet incrément — c'est la source à partir de laquelle une partie de `docs/` a été réconciliée. En cas de divergence entre `.wip/` et `docs/`, **`docs/` fait foi** ; `.wip/` garde la trace de comment on y est arrivé.

## Principes de cette documentation

- Aucune fonctionnalité future n'est présentée comme implémentée. Le statut de chaque spec dans [`specifications/catalog.yml`](specifications/catalog.yml) reflète l'état réel du code fusionné dans `main`, pas l'intention.
- Une branche sans contenu réel (diff vide ou négatif face à `main`) n'est jamais présentée comme du travail réalisé.
- Toute décision structurante vit dans [`adr/decisions-log.md`](adr/decisions-log.md) — si elle n'y est pas, elle n'est pas actée, seulement envisagée.
- La cible de déploiement AWS reste `candidate` (ECS Fargate Linux) tant que le spike de validation correspondant n'a pas conclu — voir [`specifications/spikes/spike-01-ecs-fargate-linux-viability.md`](specifications/spikes/spike-01-ecs-fargate-linux-viability.md).
- CoreAPI est et reste un **resource server** : aucune documentation ici ne décrit CoreAPI émettant un token.

## Historique de cet incrément

Cette arborescence a été créée par l'incrément **EN-01 — Baseline documentaire, architecture et catalogue des spécifications** (2026-07-19), à partir du contenu déjà présent dans le dépôt (README racine, `.wip/docs/`, historique Git, revue d'architecture/sécurité du 2026-07-19), corrigée en plusieurs passes, puis **validée explicitement par Philippe le 2026-07-20** — statut `done`. Voir [`specifications/enablers/en-01-documentation-baseline.md`](specifications/enablers/en-01-documentation-baseline.md) pour le détail de cet incrément, et [`adr/decisions-log.md`](adr/decisions-log.md) pour la décision de validation elle-même.
