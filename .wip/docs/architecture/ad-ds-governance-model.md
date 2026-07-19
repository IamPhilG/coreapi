---
name: ad-ds-governance-model
description: Modèle de gouvernance multidimensionnel pour l'administration d'objets AD DS via coreapi — classification de la cible, de l'intermédiaire, du chemin, du geste, de la portée et des conditions
metadata:
  type: architecture
  spec: cross-cutting (informe Specs 4-8)
  status: design — rien n'est implémenté, ce document capture le modèle avant code
---

# Modèle de gouvernance AD DS — coreapi

## Statut

Document de conception, pas de rétrospective. Capture les décisions prises en session le
2026-07-18, en complément de [authorization-and-access-model.md](authorization-and-access-model.md).
À corriger/affiner au fil de la relecture — rien ici n'est figé tant que ce statut n'est pas
passé à `validated`.

## Pourquoi un document séparé

`authorization-and-access-model.md` couvre l'axe plan EAM + chemin d'exécution technique +
mécanisme de vérification (claim JWT vs API-à-API) + credential/réseau/audit. Il ne couvre pas
la **classification de l'objet AD lui-même** ni la décomposition du **geste métier** en droits
AD élémentaires — deux axes qui se sont révélés nécessaires en essayant de nommer un simple
scope OAuth2 pour Spec 4. Ce document couvre ces axes, plus la reconciliation de plusieurs
axes qui semblaient d'abord se recouvrir et qui ne se recouvrent pas.

## Corpus de référence

Le schéma AD est la source de vérité technique — il définit chaque classe d'objet créable
dans la forêt, ses attributs obligatoires/facultatifs, et les règles qui encadrent son usage.
À étudier/référencer au fur et à mesure de la construction du modèle :

- Le modèle de données et le Directory Information Tree (DIT)
- Le schéma Active Directory et les spécifications techniques MS-ADTS
- Le modèle historique de séparation administrative par tiers
- L'Enterprise Access Model (EAM)
- Le Rapid Modernization Plan (RaMP)
- Les mécanismes de délégation : ACL, ACE, droits étendus, property sets, écritures validées

Voir aussi la fiche knowledge-base `enterprise-access-model` (`.wip/kb`) pour la définition
générique des trois plans EAM.

## Principe fondateur : quatre axes indépendants, pas un seul

Erreur corrigée en session : le "plan du geste" n'est **pas** un axe Microsoft autonome. Le
Management Plane décrit le rôle architectural et le périmètre de contrôle d'un **système**
(l'application appelante), pas la nature grammaticale d'une action. Une application peut être
un composant du Management Plane tout en effectuant, à un instant donné, un geste sur une
cible Tier 2 — les deux classifications ne se déduisent pas l'une de l'autre.

Quatre axes à évaluer indépendamment pour chaque requête :

1. **Classification de l'objet cible** — son tier
2. **Classification de l'application/intermédiaire d'exécution** — son propre plan/tier,
   potentiellement différent du tier de la cible qu'il manipule à un instant donné
3. **Chemin d'accès** — User Access, Application Access, ou Privileged Access
4. **Geste autorisé** — la capacité métier demandée, décomposée en droits AD élémentaires

### Plans ≠ chemins d'accès — et PAM n'est ni l'un ni l'autre

Distinction à ne pas perdre : les **plans** (Control / Management / Data-Workload) décrivent
*où vit l'actif et son niveau de contrôle*. Les **chemins d'accès** (User Access / Application
Access / Privileged Access) décrivent *comment on l'atteint*. Ce sont deux classifications
différentes, pas une correspondance 1-pour-1 (déjà noté dans
`authorization-and-access-model.md` pour les plans ; s'applique symétriquement aux chemins).

**PAM (ex. CyberArk) n'est ni un quatrième plan, ni le nom d'un chemin.** C'est un
**intermédiaire de sécurité** qui contrôle un Privileged Access Path — un point de passage
obligé (checkpoint/broker) sur ce chemin, pas une catégorie de classification en soi.

### Une opération peut traverser plusieurs segments, avec un chemin différent à chaque segment

Exemple de référence : un opérateur humain accède à une application d'administration par un
**Privileged Access Path contrôlé par PAM** (segment 1) ; cette application exécute ensuite la
modification dans AD par un **Application Access Path**, avec une identité technique (segment
2). Les deux segments ont chacun leur propre chemin, potentiellement leur propre plan/tier
d'intermédiaire, leurs propres conditions.

**Position de coreapi dans la chaîne** : coreapi n'occupe jamais que le **dernier segment**
(application → AD), toujours avec une identité technique (gMSA — voir
`authorization-and-access-model.md`). coreapi ne voit jamais directement un segment amont
impliquant PAM ou un opérateur humain — il ne reçoit qu'un JWT dont il doit faire confiance
qu'un processus de gouvernance en amont (potentiellement via PAM, potentiellement via
IIQ/ServiceNow) a déjà validé les segments précédents. Cohérent avec le principe déjà posé :
coreapi applique la politique, il ne négocie pas avec les intermédiaires qui l'ont précédé.

**Correction (relecture du 2026-07-18)** : "toujours Application Access" décrivait à tort ce
segment comme jamais privilégié. C'est une confusion entre deux propriétés indépendantes :

- **Access modality** (comment coreapi s'authentifie) : toujours Application Access — une
  identité technique/machine, jamais une identité humaine. Ça ne change jamais, pour aucun des
  trois profils d'exécution.
- **Privilege class** (la sensibilité de ce que ce segment autorise à faire) : varie selon la
  cible **et selon le geste** (voir correction plus bas — même une cible Tier 2 donne lieu à
  une classe Privileged dès que le geste modifie l'état, pas seulement pour du Tier 0/Control
  Plane). Une identité machine (Application Access) peut parfaitement exécuter une opération
  privilégiée — c'est précisément le rôle du profil `control-plane-operations` décrit dans
  `authorization-and-access-model.md` : Application Access dans sa modalité, mais Privileged
  dans sa classe, avec les garanties additionnelles (JIT, réseau dédié) que ça implique.

Ces deux propriétés doivent être portées séparément dans le modèle — pas fusionnées en un seul
champ "chemin."

Le modèle d'autorisation doit donc caractériser séparément, **par segment** :
- le plan et le tier de la cible ;
- le chemin utilisé par l'acteur initiateur de ce segment ;
- l'éventuel intermédiaire PAM sur ce segment ;
- l'identité qui exécute réellement l'opération sur ce segment ;
- la portée et le geste autorisés sur ce segment ;
- les conditions d'élévation, d'approbation et de traçabilité propres à ce segment.

Conséquence pour l'audit (voir brique 7) : la trace produite par coreapi ne couvre que son
propre segment. Une corrélation de bout en bout à travers les segments amont (PAM, IIQ,
ServiceNow) suppose un identifiant de corrélation partagé et propagé par ces systèmes — non
confirmé aujourd'hui, voir "Ouvert, non résolu."

### Exemple travaillé — Spec 4 (comptes utilisateurs standards)

**Correction (relecture du 2026-07-18)** : la classe de privilège se détermine **par geste**,
pas une fois pour toutes pour la ressource "users". `create`/`update`/`delete` modifient l'état
de l'identité — ce sont des opérations d'administration privilégiée Tier 2, pas des
consultations ordinaires. Seul `read` peut relever d'une classe standard/non privilégiée, et
seulement pour un sous-ensemble d'attributs non sensibles (non implémenté aujourd'hui,
`UserDto` expose un ensemble fixe — voir briques 5/6).

| Axe | `read` | `create` / `update` / `delete` |
|---|---|---|
| Application (intermédiaire) | Management Plane éventuel — propriété de l'app, pas de la cible | idem |
| Access modality | Application Access | Application Access |
| Privilege class | Standard/non-privilégié (sous réserve de restriction d'attributs, pas encore faite) | **Privileged administration — Tier 2** |
| Cible | Tier 2 | Tier 2 |
| Geste | Consultation d'identité | Identity Lifecycle (Joiner-Mover-Leaver) — étiquette descriptive, absente du nom du scope (voir plus bas) |

Règle d'alerte : si l'application peut aussi gérer des comptes privilégiés, des groupes
administratifs, des politiques d'authentification, ou des autorisations permettant une
élévation, elle devient elle-même un intermédiaire du Control Plane/Tier 0 — l'évaluation de
l'axe 2 n'est donc pas figée une fois pour toutes pour une application donnée, elle dépend de
l'étendue de ce que cette application est habilitée à toucher **à un instant donné**.

### Classification par pouvoir maximal, pas par opération courante

Nuance importante sur la règle d'alerte ci-dessus : elle s'applique à la **classification de
l'opération en cours**, pas à la protection de l'application elle-même. Une application (ou
une instance coreapi) doit être protégée selon le **pouvoir maximal qu'elle peut exercer**, pas
selon ce qu'une requête donnée est en train de faire. Si une même instance/credential coreapi
peut, même occasionnellement, toucher une cible Tier 0 (ex. modifier une ACL), sa compromission
donne potentiellement accès au Tier 0 — elle doit donc être classée et protégée comme un actif
Tier 0 en permanence, y compris quand elle traite par ailleurs des requêtes Tier 2 routinières.

C'est cohérent avec, et renforce, la séparation déjà actée dans
`authorization-and-access-model.md` : trois comptes de service distincts, trois credentials
distincts, un profil `control-plane-operations` sans Singleton et avec JIT — cette séparation
existe précisément pour qu'aucune instance de coreapi ne cumule un pouvoir maximal Tier 0 tout
en traitant aussi du trafic Tier 2 routinier. Question ouverte non tranchée : cette séparation
par credential/DI au sein d'un même processus suffit-elle, ou faut-il aller jusqu'à des
**déploiements coreapi séparés par tier** (ex. `coreapi-t2` ne pouvant physiquement pas charger
le code/credential du profil `control-plane-operations`) ? Voir "Ouvert, non résolu."

### Nommage des scopes — décision validée le 2026-07-18

Le scope OAuth2 exprime la **cible et la capacité** (axes 1 + 4), pas le plan de
l'intermédiaire (axe 2, qui vit dans la fiche d'intégration du client, pas dans le nom du
scope) ni une reformulation du chemin (axe 3, déjà implicite : coreapi n'accepte que du
client_credentials, donc toujours de l'Application Access).

Convention : `coreapi.ad.<tier>.<ressource>.<verbe>` — pas de segment "lifecycle" : les verbes
expriment déjà la capacité, et un segment "lifecycle" classerait artificiellement `read` et
`audit` comme des opérations Joiner-Mover-Leaver alors qu'elles ne le sont pas.

**Scopes Spec 4 (validés)** :
- `coreapi.ad.t2.users.read`
- `coreapi.ad.t2.users.create`
- `coreapi.ad.t2.users.update`
- `coreapi.ad.t2.users.delete`
- `coreapi.ad.t2.users.audit`

Voir `authorization-and-access-model.md` pour la convention complète et la note sur la classe
de privilège par geste (section "Taxonomie des scopes").

## Les sept briques du modèle de gouvernance

Pour chaque brique : `spécifié`, `partiellement spécifié`, ou `à construire`.

### 1. Plan/tier de la cible — **partiellement spécifié**

Cascade de classification actée : Tier 0 ? → sinon Tier 2 ? → sinon Tier 1 ? → sinon défaut
Tier 0 (fail-secure — dans le doute, le plus restrictif).

Ce qui existe : le principe de la cascade, et une classe résolue par exception explicite
(compte utilisateur standard = Tier 2, pour Spec 4).

Ce qui manque :
- Aucun mécanisme en code ne détermine le tier d'un objet à l'exécution
- Aucune règle écrite pour les autres classes d'objets (groupes, comptes de service,
  ordinateurs, GPO, ACL/délégations, OU) — seule `user` a été traitée, et seulement pour le
  cas "standard"
- Pas de référentiel (ex. quelles OU/quels groupes marquent un objet Tier 0) — dépend du
  travail d'inventaire décrit en brique 4

### 2. Plan/tier de l'intermédiaire d'exécution — **à construire**

Ce qui existe : le principe que c'est un axe séparé de la cible (voir plus haut), la règle
d'alerte sur l'élévation (une app touchant du Control Plane devient elle-même Control Plane),
et le principe multi-segment (PAM, ou tout autre intermédiaire amont, est un segment distinct
du segment coreapi — voir plus haut).

Ce qui manque :
- Aucun champ dans la fiche d'intégration (`authorization-and-access-model.md`) ne capture
  aujourd'hui le plan/tier de l'application cliente elle-même
- Aucune vérification technique n'empêche qu'une app enregistrée comme "Tier 2 App Access
  uniquement" se voie accorder un scope touchant une cible Control Plane
- Aucune représentation des segments amont (PAM/CyberArk ou autre) dans la fiche
  d'intégration ni dans le log coreapi — coreapi ne voit et ne documente que son propre segment

### 3. Chemin d'accès — **spécifié pour l'essentiel, sur le seul segment coreapi**

Ce qui existe : coreapi n'accepte que client_credentials — jamais de flux délégué, jamais
d'identité humaine en bout de chaîne. La **modalité d'accès** du segment coreapi (application →
AD) est donc toujours Application Access, jamais User Access. La **classe de privilège** de ce
même segment, en revanche, varie avec la cible et le geste — voir la correction dans la section
précédente : Application Access n'implique pas "jamais privilégié." Les trois profils
d'exécution *techniques* internes de coreapi (`user-identity-operations` /
`service-identity-operations` / `control-plane-operations`, voir
`authorization-and-access-model.md`) correspondent à cette variation de classe de privilège au
sein d'une modalité d'accès qui reste constante — chacun apporte une combinaison de
credential/DI/réseau proportionnée à sa classe de privilège.

Ce qui manque : confirmation que le profil `control-plane-operations` de coreapi (Spec 7, ACL)
reste Application Access avec des garanties renforcées (JIT, réseau dédié), ou s'il constitue
en réalité le segment aval d'un Privileged Access Path initié en amont par un opérateur via
PAM — la réponse détermine si coreapi doit un jour vérifier lui-même une preuve issue du PAM
(API-à-API), au-delà du claim JWT.

### 4. Classe d'objet — **à construire**

Ce qui existe : Spec 4 traite `user` (compte standard) par décision explicite, au cas par cas.

Ce qui manque, intégralement :
- Un inventaire des classes AD que coreapi touchera (computer, group — sécurité/distribution/
  domaine local/global/universel —, service account traditionnel vs sMSA vs gMSA, OU,
  conteneur, GPO — attention : un GPO n'est pas un objet AD autonome, il comprend un conteneur
  AD et un template SYSVOL —, contact, objets de confiance, objets sites/sous-réseaux/
  réplication, objets schéma/configuration)
- Une distinction catégorie métier ↔ classe technique AD (ex. poste de travail, serveur
  membre et contrôleur de domaine sont tous `computer`, mais n'ont pas le même tier)
- Un mécanisme de découverte : interroger le schéma de la forêt, énumérer classes/attributs
  réellement disponibles, détecter les extensions apportées par les produits installés,
  inventorier les objets réellement présents, comparer à un référentiel connu — rien de tout
  ça n'existe. Le fait de promouvoir un DC vierge (Spec 0) donne les conteneurs par défaut
  (racine du domaine, Builtin, Users, Computers, OU Domain Controllers) mais ne donne ni la
  liste exhaustive des classes du schéma ni les objets ajoutés ultérieurement par des
  applications

### 5. Geste et droit élémentaire — **partiellement spécifié**

Ce qui existe : les quatre verbes CRUD (read/create/update/delete) sont implémentés et
protégés par scope sur `UsersController` (retrofit Spec 4, `feature/spec-4-authz-retrofit`,
scopes à renommer vers `coreapi.ad.t2.users.*` — voir plus haut). "Identity Lifecycle
(Joiner-Mover-Leaver)" reste une étiquette descriptive utile pour create/update/delete de
comptes standards, mais n'apparaît pas dans le nom du scope lui-même.

Ce qui manque :
- La décomposition en droits AD élémentaires que Microsoft distingue (permissions sur
  l'objet, permissions sur un attribut précis, droits étendus — reset password, unlock — et
  écritures validées) n'existe pas. `UpdateAsync` est aujourd'hui une opération générique
  "modifier plusieurs champs", pas une collection d'actions élémentaires alignées sur les
  droits AD réels
- Pas de mapping "geste métier → opérations AD élémentaires qu'il requiert" (ex. un "Joiner"
  = create + placement OU + appartenances de groupe initiales — aujourd'hui `CreateAsync` ne
  gère pas l'appartenance de groupe)

### 6. Portée — **partiellement spécifié**

Ce qui existe : un contrôle domaine/sous-arbre — `EnsureWithinConfiguredBaseDn` refuse toute
opération hors du base DN configuré (`UserService.cs`).

Ce qui manque : toute granularité plus fine — portée par OU spécifique (ex. "cette app ne gère
que `OU=Contractors`"), par objet individuel, par attribut, par property set, ou par droit
étendu précis. Le contrôle actuel est binaire (dans le périmètre configuré ou non), pas
hiérarchique.

### 7. Conditions d'exécution — **partiellement spécifié**

Ce qui existe (voir `authorization-and-access-model.md`) : validation stricte du JWT
(signature/issuer/audience/algorithme), détection de réutilisation par `jti`, format de log
SIEM déjà défini, contrôle à deux jetons (IIQ + ServiceNow) pour la création/modification
d'une fiche d'intégration.

Ce qui manque : élévation JIT par geste, consommation d'une approbation par action (out of
scope coreapi par décision explicite — vit dans la gouvernance externe), séparation des tâches
appliquée en code (out of scope coreapi, décision de gouvernance), contrainte de poste/PAW
d'origine, mécanisme de compensation/retour arrière en cas d'échec partiel.

## Rôle de coreapi (pipeline)

Reformulé depuis le principe déjà posé dans `authorization-and-access-model.md`
("coreapi est un point d'application de la politique, pas un moteur de décision métier"),
étendu à la classification d'objet :

1. Identifier l'acteur (l'application appelante) et son rôle
2. Identifier précisément l'objet cible
3. Déterminer le plan, le tier, la portée et la classification de la cible
4. Traduire le geste métier demandé en opérations AD élémentaires
5. Évaluer la règle de gouvernance correspondante
6. Vérifier les conditions d'accès et les éventuelles approbations
7. Utiliser une identité d'exécution disposant uniquement des droits nécessaires
8. Exécuter l'opération
9. Vérifier le résultat
10. Produire une trace complète et exploitable
11. Déclencher, si nécessaire, une procédure de compensation/retour arrière

Étapes 1, 6 (partiellement), 7 (partiellement), 8, 9 (implicitement, via les exceptions
`NotFoundException`/`ConflictException`), 10 (schéma défini, pas branché) sont amorcées.
Étapes 3, 4, 5, 11 sont entièrement à construire.

## Ouvert, non résolu

- **Inventaire des classes d'objet AD** — aucun travail commencé, prérequis à toute extension
  au-delà de Spec 4
- **Mécanisme de détermination du tier à l'exécution** — cascade actée en principe, aucune
  implémentation, aucun référentiel
- **Champ "plan/tier de l'intermédiaire" dans la fiche d'intégration** — absent du schéma
  actuel de la fiche (`authorization-and-access-model.md`)
- **Décomposition des gestes en droits AD élémentaires** — `UpdateAsync` notamment reste une
  opération non décomposée
- **Corrélation d'audit inter-segments** — coreapi ne journalise que son propre segment
  (application → AD) ; relier cette trace à un éventuel segment PAM/opérateur amont suppose un
  identifiant de corrélation partagé et propagé par ces systèmes externes, non confirmé
- **Statut EAM du profil `control-plane-operations` de coreapi** — Application Access
  renforcé, ou segment aval d'un Privileged Access Path amont nécessitant une vérification
  API-à-API vers le PAM ?
- **Déploiements coreapi séparés par tier** — la séparation actuelle (comptes de
  service/DI/réseau distincts au sein d'un même processus) suffit-elle, ou le pouvoir maximal
  d'une instance capable de toucher du Tier 0 justifie-t-il des déploiements physiquement
  séparés (`coreapi-t2`/`coreapi-t1`/`coreapi-t0`) ? Coût d'infra/ops non négligeable si oui —
  à trancher, pas à supposer.
- **Champs de classification dans la fiche d'intégration** — la fiche décrite dans
  `authorization-and-access-model.md` (`appId`/`status`/`claimMapping`/`piiDisclosureLevel`/
  `siemExtensionFields`) ne porte aujourd'hui ni le plan/tier de l'intermédiaire (brique 2),
  ni un éventuel indicateur `pamRequired` pour les capacités qui l'exigeraient. À réconcilier
  entre les deux documents avant implémentation, pas à dupliquer.
