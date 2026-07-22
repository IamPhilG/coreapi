---
id: SEC-06
title: Protection des chemins d'attaque — contrats d'intégration
type: security
status: planned
last_reviewed: "2026-07-22"
---

# SEC-06 — Protection des chemins d'attaque (contrats d'intégration)

## Statut réel

**Planned.** Aucun code correspondant n'existe. Créé le 2026-07-22 pour enregistrer une **surface de
contrat future** et empêcher qu'elle soit improvisée dans un incrément de code.

Ce que cet item couvre : les trois contrats d'intégration, agnostiques du fournisseur, définis dans
[`../../architecture/views/attack-path-protection-integration.md`](../../architecture/views/attack-path-protection-integration.md)
et [`../../architecture/contracts/`](../../architecture/contracts/).

Ce que cet item **ne couvre pas**, explicitement :

- l'intégration à un produit d'analyse de chemins d'attaque, quel qu'il soit ;
- la production d'une simulation de graphe — capacité distincte, hors du cœur de CoreAPI ;
- le choix du fournisseur, des zones privilégiées, des seuils et de la politique de continuité —
  décisions d'adoption de l'organisation, hors de ce dépôt ;
- toute extension du périmètre de **COREAPI-04b** (voir « Impacts sur COREAPI-04b » ci-dessous).

## Implémenté

Aucun.

## Vérifié

Aucun.

## Position architecturale (rappel, non dupliqué)

CoreAPI reste **fournisseur-agnostique** et **point d'application de la politique** : il exprime un
changement proposé, consomme un résultat d'évaluation produit ailleurs, applique le comportement
décisionnel déclaré par l'organisation, et consigne la décision. Il ne produit ni analyse de graphe,
ni simulation, ni verdict propre. Détail :
[`../../architecture/views/attack-path-protection-integration.md`](../../architecture/views/attack-path-protection-integration.md).

## Dépendances

- `depends_on` : [SEC-01](sec-01-authorization-by-client-ou-object-attribute.md) — un changement
  proposé ne peut être exprimé de façon fiable que si la cible est classifiée et la portée maîtrisée
  (briques 1, 4, 6 du modèle d'autorisation). Sans SEC-01, `ProposedDirectoryChange` ne peut pas
  décrire correctement `target` ni `intendedEffects`.
- `depends_on` : [SEC-03](sec-03-audit-correlation-non-repudiation.md) — la conservation de la
  décision de risque suppose la traçabilité et l'identifiant de corrélation, non implémentés.
- `blocks` : **aucun**. Valeur alignée avec [`catalog.yml`](../catalog.yml), où `SEC-06` porte
  `blocks: []`. Aucune deuxième vérité n'est entretenue ici.
- `constrains` : [SPEC-6](../functional/spec-6-groups-and-ous.md) et
  [SPEC-7](../functional/spec-7-acl-and-delegations.md) — ces specs touchent directement aux
  appartenances et aux délégations, c'est-à-dire aux gestes qui créent ou étendent des chemins
  d'attaque. **`constrains` n'est pas `blocks`** : SEC-06 ne gèle ni SPEC-6 ni SPEC-7 et ne
  conditionne pas leur démarrage. Il pose une contrainte de conception — aucune des deux ne devrait
  être implémentée sans que la question « ce geste crée-t-il un chemin ? » ait reçu une réponse
  **déclarée**, fût-elle « aucune capacité déclarée ». `constrains` est une relation documentaire :
  elle n'existe pas dans le schéma de `catalog.yml` et n'y est donc pas reportée.

## Incréments à réaliser

Aucun n'est engagé. Ordre proposé, pas planifié :

1. **SEC-06.a — Geler les contrats.** Revue et validation des trois schémas JSON tels qu'ils sont, en
   version `1.0`. Aucun code. Livrable : décision de validation dans
   [`../../adr/decisions-log.md`](../../adr/decisions-log.md).
2. **SEC-06.b — Déclarer le profil de CoreAPI.** Une valeur de `AttackPathProtection` (ensemble de
   capacités, fournisseur, comportement, seuil de fraîcheur, politique de continuité) déclarée pour
   CoreAPI. Valeur attendue au départ : aucune capacité, `providerKind: None`,
   `behavior: InformOnly` — la déclaration honnête d'une absence, pas une conformité.
3. **SEC-06.c — Consigner la décision de risque.** Enregistrer l'évaluation et la décision dans le
   flux de preuves, y compris lorsque l'évaluation est `Unknown` ou absente. Dépend de SEC-03.
4. **SEC-06.d — Point d'extension d'évaluation.** Un point d'extension interne, sans implémentation
   de fournisseur, permettant de brancher un évaluateur externe. À ne pas ouvrir avant que SEC-06.a
   et SEC-06.b soient acquis.

## Critères d'acceptation

Pour SEC-06.a (le seul incrément dont les critères sont définissables aujourd'hui).

**Les critères sont répartis en quatre catégories de contrôle**, parce qu'ils ne sont pas vérifiables
par le même moyen. Confondre ces catégories reviendrait à prêter à JSON Schema des capacités qu'il
n'a pas.

| Catégorie | Ce qu'elle couvre | Par quoi |
| --- | --- | --- |
| **A — structure** | Syntaxe et forme d'un document : types, champs obligatoires, énumérations, `additionalProperties`. | JSON Schema Draft 2020-12 |
| **B — cohérence conditionnelle** | Dépendances **à l'intérieur d'un même objet** : « si ce champ vaut X, alors cet autre champ est requis / interdit / contraint ». | JSON Schema Draft 2020-12 (`if`/`then`, `const`, `not`) |
| **C — sémantique chronologique** | Comparaison de **deux horodatages entre eux**. | Validateur sémantique de pipeline ou d'application |
| **D — croisé entre documents** | Comparaison d'une valeur de ce document avec une valeur d'un **autre** document, et résolution d'une référence. | Validateur sémantique de pipeline ou d'application |

**JSON Schema ne compare pas deux dates et ne lit pas un second document.** Les catégories C et D ne
sont donc jamais assurées par les schémas ; elles sont assurées par un validateur distinct, et cette
répartition doit rester explicite partout où les critères sont cités.

### Catégorie A — structure (JSON Schema)

| # | Critère | Vérification |
| --- | --- | --- |
| A1 | Les trois schémas sont valides en Draft 2020-12 et compilent. | Compilation par un validateur 2020-12. |
| A2 | Aucun schéma n'accepte de chemin d'attaque détaillé (arêtes, principaux intermédiaires). | `findings` agrégé ; `additionalProperties: false` partout. |
| A3 | **Aucun nom de fournisseur n'apparaît nulle part dans les trois schémas.** L'évaluateur est désigné par `providerId` opaque et `providerKind` générique. | Recherche textuelle : zéro occurrence d'un nom d'éditeur. |
| A4 | `AttackPathAssessment` exige `capability`, `providerId` et `providerKind` ; `certainty` **refuse** `Verified`. | Instances invalides rejetées. |
| A5 | `ProposedDirectoryChange` exige `writeIntent`, `proposedBy`, et distingue `callingApplication` / `originatingActor` / `proposedBy`. | Instances invalides rejetées. |
| A6 | Toute référence obligatoire porte `minLength: 1` — aucune chaîne vide ne passe. | Instance à `assessmentRef: ""` rejetée. |
| A7 | Aucun fichier sous `src/`, `tests/` ou `tools/` n'est modifié par cet incrément. | Diff de l'incrément. |

### Catégorie B — cohérence conditionnelle dans un même objet (JSON Schema)

| # | Critère | Vérification |
| --- | --- | --- |
| B1 | `Observed` exige `capability = CurrentStateAssessment`, `dataCollectedAt`, `exposureMeasure`, `freshnessThresholdMet = true` et `targetCovered = true`. | Instances périmées ou à cible non couverte rejetées. |
| B2 | `Simulated` exige `capability = ProposedChangeSimulation`, un état observé de départ, la version du modèle, les types de relations modélisés et une mesure. | Instances incomplètes rejetées. |
| B3 | `Unknown` exige une raison structurée et **interdit** une mesure numérique. | Instance `Unknown` portant `exposureMeasure` rejetée. |
| B4 | `AttackPathValidationResult` interdit `certainty: Verified` avec `conformity: Inconclusive`. | Instance rejetée. |
| B5 | `Verified` exige `assessmentRef` non vide, `dataCollectedAt`, l'attestation de recollecte postérieure, cible couverte, complétude complète, attestation de comparabilité, `measureBefore` **et** `measureAfter`. | Instance amputée d'un seul de ces champs rejetée. |
| B6 | `conformity: Divergent` exige une qualification non vide. | Instance sans `divergenceNote` rejetée. |
| B7 | `originatingActor` : `Verified` et `AssertedNotVerified` exigent `actorRef` et `assertedBy` ; `Absent` les **interdit**. | Les trois cas testés. |
| B8 | `governanceReference`, lorsqu'elle est présente, exige `referenceType` et un `referenceId` non vide. | Instances rejetées. |
| B9 | Une opération d'écriture exige `writeIntent = true`. | `writeIntent` absent ou `false` rejeté. |
| B10 | `proposedBy.origin = AiAssisted` exige une `proposalRef`. | Instance rejetée. |

### Catégorie C — sémantique chronologique (hors JSON Schema)

| # | Critère | Vérification |
| --- | --- | --- |
| C1 | `revalidatedAt` est postérieur à `executedAt`. | Validateur sémantique. |
| C2 | Pour `Verified`, `dataCollectedAt` est postérieur à `executedAt` — **la recollecte suit le geste**. | Validateur sémantique. |
| C3 | L'attestation `recollectionAfterChange: true` n'est pas contredite par les horodatages réels. | Validateur sémantique. |

**C2 est le contrôle le plus important de tout le contrat** : sans lui, une collecte *antérieure* au
changement pourrait être présentée comme une validation. Le schéma porte l'attestation booléenne
précisément parce qu'il ne sait pas comparer les dates lui-même ; l'attestation ne remplace pas le
contrôle, elle le rend vérifiable.

### Catégorie D — croisé entre documents (hors JSON Schema)

| # | Critère | Vérification |
| --- | --- | --- |
| D1 | `assessmentRef` résout vers une évaluation réellement existante. | Validateur sémantique. |
| D2 | `measureName`, `measureDefinitionRef` et `measureVersion` sont **identiques** à ceux de l'évaluation préalable. | Validateur sémantique. |
| D3 | L'attestation `measuresComparable: true` n'est pas contredite par la comparaison réelle. | Validateur sémantique. |

Les critères des incréments b, c et d ne sont pas définissables tant que SEC-06.a n'est pas validé.

## Impacts sur COREAPI-04b

**Périmètre de COREAPI-04b, tel que fixé par Philippe le 2026-07-22** (donnée de session ; cet
incrément n'a pas d'entrée propre dans `catalog.yml` à ce jour) :

1. transport LDAPS ;
2. confiance du certificat ;
3. chaîne de confiance fournie par l'environnement ;
4. preuve cryptographique ;
5. cible RWDC.

**Ce périmètre n'est pas élargi par SEC-06.** La protection des chemins d'attaque, l'assistance IA et
tout produit d'analyse tiers restent **hors de COREAPI-04b**, sans exception.

Deux points du présent document touchent des sujets voisins de COREAPI-04b sans en faire partie :

- **Cible RWDC** : le champ `writeIntent` de `ProposedDirectoryChange` déclare seulement si le geste
  modifie l'état de l'annuaire. Le choix effectif du contrôleur, l'exigence d'inscriptibilité et le
  comportement de bascule sont une **politique de déploiement**, portée par le corpus de gouvernance
  transverse et par COREAPI-04b — pas par ce contrat, qui reste descriptif.
- **Transport et confiance du certificat** : entièrement hors de SEC-06. Ce document ne décrit ni
  transport, ni ancre de confiance, ni validation de chaîne, et ne modifie pas
  [SEC-05](sec-05-ldap-policy-and-certificates.md).

Contraintes explicites, valables tant que SEC-06 reste `planned` :

| # | Contrainte | Raison |
| --- | --- | --- |
| I1 | **Aucune intégration à un produit d'analyse de chemins d'attaque.** Pas de client HTTP, pas de point de terminaison, pas de credential associé. | Le fournisseur n'est pas décidé, ses capacités réelles ne sont vérifiées nulle part. |
| I2 | **Aucun type C# des trois contrats n'est introduit.** Ils restent des schémas JSON documentaires. | Un type introduit devient une surface à maintenir avant qu'une décision existe. |
| I3 | **Aucun comportement décisionnel n'est codé.** Ni blocage, ni approbation, ni accusé de réception liés au risque de chemin d'attaque. | Le comportement est déclaré par l'organisation, pas décidé par CoreAPI. |
| I4 | **Aucun champ de graphe d'identité dans les journaux ou les réponses.** | `ADDS-AP-006` : le graphe est une carte d'attaque ; sa propagation est un risque en soi. |
| I5 | **Aucune capacité de simulation.** | La simulation appartient à un module séparé, pas au cœur de CoreAPI. |
| I6 | Si COREAPI-04b touche la traçabilité, **ne pas introduire de champ propre à la protection des chemins d'attaque** ; conserver `correlationId` générique. | Éviter de figer un schéma d'audit sur un besoin non décidé. |
| I7 | **Aucune fonctionnalité RODC ni Cloud Kerberos Trust.** L'exigence RWDC de COREAPI-04b est une contrainte de ciblage d'écriture, pas l'amorce d'une gestion de ces sujets. | Périmètre borné explicitement. |

**Impact positif unique et suffisant** : si COREAPI-04b consolide l'identifiant de corrélation et le
flux de preuves (dans la continuité de COREAPI-04a), il rend SEC-06.c réalisable plus tard **sans
travail supplémentaire d'audit**. Aucune action n'est requise pour cela au-delà de ce qui est déjà
dans son périmètre : c'est une conséquence, pas une extension.

## Preuves

Aucune (planifié). Références :
[`../../architecture/views/attack-path-protection-integration.md`](../../architecture/views/attack-path-protection-integration.md),
[`../../architecture/contracts/`](../../architecture/contracts/).

## Prochaines étapes

Revue par Philippe. Aucun incrément ne démarre avant que SEC-06.a soit explicitement validé, et
qu'une décision d'adoption existe au niveau du portefeuille (fournisseur, zones privilégiées, seuils,
responsabilités, continuité) — décision qui n'appartient pas à ce dépôt.
