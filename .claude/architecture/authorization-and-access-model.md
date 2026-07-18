---
name: authorization-and-access-model
description: Modèle d'autorisation et d'accès de coreapi, aligné sur l'Enterprise Access Model (EAM) de Microsoft
metadata:
  type: architecture
  spec: cross-cutting (informe le retrofit de Spec 4 et la conception de Specs 5-8)
  status: design — rien n'est implémenté, ce document capture les décisions prises avant code
---

# Modèle d'autorisation et d'accès — coreapi

## Statut

Document de conception, pas de rétrospective. Capture les décisions prises en session le
2026-07-18, avant tout code. À corriger/affiner au fil de la relecture — rien ici n'est figé
tant que ce statut n'est pas passé à `validated`.

## Pourquoi ce document existe

L'audit Codex du 2026-07-17 (voir les commits `a48f375`, `8009b2a`, `052713c`) a trouvé que
coreapi n'a aujourd'hui aucune autorisation au-delà de "le token JWT est-il valide" —
n'importe quel appelant avec un token signé par l'issuer/audience configurés peut créer,
modifier, ou supprimer n'importe quel objet AD dans le périmètre du compte de service. Ce
document définit le modèle qui corrige ça, avant que Specs 5/6/7/8 ne soient construites sur
une fondation à refaire.

## Principe fondateur : coreapi est un point d'application de la politique, pas un moteur de
## décision métier

coreapi n'est **pas** une interface utilisateur, ne gère pas de mots de passe utilisateur
final, et n'implémente **jamais** :
- le workflow d'approbation lui-même (qui approuve quoi, simple ou double approbation) —
  ça vit dans les outils de gouvernance (IIQ, ServiceNow, ou autre selon le flux)
- la logique métier de décision de sécurité — "le métier décide de la sécurité et des besoins
  de sécurité technique d'Active Directory," coreapi applique cette décision, ne la prend pas
- l'intégration profonde avec HashiCorp Vault — Vault est présupposé exister en dehors du
  périmètre de coreapi ; coreapi n'implémente que le **mécanisme de déclenchement** (Vault ne
  peut pas deviner tout seul qu'une action est sur le point de se produire)

Ce que coreapi fait systématiquement, sans exception, pour **chaque** action :
1. Vérifie qu'une preuve d'autorisation valide accompagne la requête
2. Exécute l'action avec le niveau de privilège technique approprié
3. Laisse une trace complète et auditable — non-répudiation, non-shadow-IT, rien
   d'ungoverned

## Les trois plans (EAM), pas trois chemins

*Définition générique du modèle EAM : voir `.wip/kb` `enterprise-access-model`. Ce qui
suit est uniquement le mapping propre à coreapi.*

Erreur initiale corrigée en session : un plan (où vit l'actif) et un chemin d'accès (comment
on l'atteint/le gouverne) sont deux axes différents — pas la même chose, pas une
correspondance 1-pour-1.

| Plan EAM | Ce que coreapi y place | Specs coreapi concernées |
|---|---|---|
| **Data/Workload** | Comptes utilisateurs (Spec 4), comptes de service (Spec 5), groupes/OU ordinaires (Spec 6) — humain ou compte de service, peu importe | Spec 4, 5, 6 |
| **Management** | N/A directement — c'est la source d'autorisation (IIQ, ServiceNow, PAM), pas une cible AD | — |
| **Control** | Domain Admins, Enterprise Admins, Schema Admins, structure des ACL elle-même | Spec 7 (ACL), toute action touchant un groupe/objet Tier 0 |

Point clé, initialement mal compris puis corrigé : **le Data/Workload plane est gouverné par
le Management plane** — un compte de service (Data/Workload) et un utilisateur standard
(Data/Workload) ne sont pas gouvernés différemment par nature ; les deux passent par une
gouvernance Management Plane, mais **cette gouvernance n'a pas une source unique**.

## Gouvernance à sources multiples — pas de passage obligé par un seul outil

Contrairement à une première hypothèse (tout passe par IIQ), plusieurs sources de preuve
d'autorisation coexistent selon le cas d'usage :
- **IIQ** — pour les flux qui exigent une gouvernance humaine explicite
- **Ticket ServiceNow** — validation suffisante pour certains flux (ex. une application qui a
  un besoin légitime et documenté de créer des comptes de service)
- **Scope pré-autorisé / self-service** — pour des cas d'usage comme la création de comptes
  de service ou la gestion de groupes "à la volée" par une application, où exiger un passage
  systématique par le Management Plane à chaque appel n'est pas réaliste opérationnellement

coreapi n'a pas besoin de savoir parler à IIQ, ServiceNow, ou tout futur outil de gouvernance.
Il a besoin d'une façon **standard** de recevoir et vérifier une assertion d'autorisation,
quelle que soit la source qui l'a produite.

## Mécanisme de vérification — proportionnel à l'enjeu, pas uniforme

**Principe Zero Trust visé, non atteint en V1** : dans l'idéal, chaque appel serait vérifié
en direct (API-à-API, coreapi vers l'API de la source d'autorisation). Pas réaliste comme
exigence uniforme pour la V1 — c'est une direction, pas une obligation immédiate. Décision
explicite : **rien à coder maintenant** — aucune spec actuelle (4 est déjà écrite, 5/6/7 pas
commencées) n'atteint encore le Control Plane, donc aucun code n'exerce ce chemin aujourd'hui.
Cette section capture la décision pour que Spec 7 la respecte dès sa conception.

| Niveau | Mécanisme de vérification | Justification |
|---|---|---|
| Data/Workload (Spec 4, 5, 6) | **Claim signé dans le JWT** — coreapi vérifie la signature et la présence du claim d'autorisation, ne valide pas le processus métier derrière | Le coût d'une vérification API-à-API systématique dépasse le bénéfice pour ce volume d'appels routiniers |
| Control Plane / PAM / Tier 0 (Spec 7 et au-delà) | **Vérification API-à-API en direct**, coreapi vers l'API de la source d'autorisation | L'enjeu justifie la complexité additionnelle — c'est précisément le principe de proportionnalité de la sécurité, pas une négligence pour les autres niveaux |

## Claim de confiance client et fiche d'intégration

**Décision (résout le point "forme du claim JWT — Option A")** : pas de nouveau claim
personnalisé. Réutilisation du claim `scope`/`scp` OAuth2 standard, déjà présent dans le
pipeline de validation Spec 3 (confirmé sur un jeton Okta réel en session : `"scp":
["coreapi.access"]`).

### Taxonomie des scopes — nommage hiérarchique, construction incrémentale

Risque identifié : une combinatoire (verbe × ressource × tier/sensibilité × chemin) explose
vite si on tente de l'énumérer d'un coup. Décision : ne pas construire toute la taxonomie
maintenant — définir une **convention de nommage** stable, et n'instancier que les scopes
réellement nécessaires, spec par spec, branche par branche, au fur et à mesure du besoin.

**Convention** : `coreapi.<plan>.<chemin>.<ressource>.<verbe>`

- `<plan>` — un des trois plans EAM (`data-workload`, `management`, `control`)
- `<chemin>` — un des chemins d'exécution technique (`standard`, `applicatif`, `privilegie`)
- `<ressource>` — le type d'objet concerné (`users`, `groups`, `ous`, `service-accounts`,
  `acl`, etc.)
- `<verbe>` — un des cinq verbes retenus : `read` (Get/List), `create`, `update`, `delete`,
  `audit` (accès à la trace d'audit de la ressource, distinct de la lecture de son état
  courant)

Les deux premiers segments (`<plan>`, `<chemin>`) sont deux axes **indépendants**, pas
redondants : un même type de ressource peut relever de plans différents selon la sensibilité
de sa cible précise (ex. création d'un groupe ordinaire = Data/Workload, création d'un groupe
Tier 0 = Control Plane, alors que les deux passent par le même type d'objet "groupe"). Le plan
n'est donc pas déductible du chemin seul. Ce mécanisme — le scope déclare à l'émission du
jeton sur quel plan le client est autorisé à agir, plutôt que coreapi n'inspecte la cible à
l'exécution pour en déduire sa sensibilité — est la réponse de mécanisme au point "Spec 6 et
la sensibilité de l'objet cible" (Spec 6 reste à construire pour l'appliquer).

**Limite technique à connaître** : les IdP usuels (Okta, Entra) ne font pas de correspondance
par préfixe/wildcard sur les scopes — chaque scope est une chaîne exacte, accordée
individuellement à un client. La hiérarchie ci-dessus est donc une convention de nommage utile
pour l'audit et la lisibilité, pas un mécanisme natif de l'IdP. Des policies "grossières"
basées sur un préfixe resteraient à implémenter côté coreapi lui-même, si/quand le besoin
apparaît — pas fait maintenant.

**Séparation des tâches (SoD)** : coreapi vérifie chaque scope **indépendamment, sans
inférence** — détenir `create` n'implique jamais `delete`, ni aucun autre verbe. La décision
de quelles combinaisons de scopes peuvent être accordées ensemble à un même client est une
politique de gouvernance (IIQ), hors périmètre technique de coreapi — cohérent avec le
principe fondateur du document.

**Premiers scopes concrets (Spec 4, chemin Standard, ressource `users`)** :
`coreapi.data-workload.standard.users.read`, `...create`, `...update`, `...delete`,
`...audit`.

### Qui émet le claim, et comment coreapi l'interprète

L'IdP (Okta/Entra ou autre) émet le claim au moment de l'enregistrement du client appelant —
mécanisme déjà exercé en session (assignation du scope `coreapi.access` à l'app de test dans
Okta). Mais coreapi opère avec plusieurs consommateurs, potentiellement plusieurs IdP, qui ne
nomment pas forcément leurs scopes de la même façon que coreapi. D'où la **fiche
d'intégration** : un registre, un enregistrement par consommateur, qui traduit les claims
entrants de ce consommateur vers l'ensemble d'attributs canoniques de coreapi.

Contenu de la fiche :

| Champ | Rôle |
|---|---|
| `appId` | Identifiant unique du consommateur (client_id), assigné à l'onboarding, immuable |
| `status` | Active / Inactive / Décommissionnée — statut **technique** de l'habilitation à appeler coreapi, distinct de tout statut métier |
| `claimMapping` | Règles traduisant le(s) claim(s) entrant(s) de ce consommateur vers les valeurs canoniques ci-dessus |
| `piiDisclosureLevel` | GUID (défaut) ou DN (opt-in) — voir schéma du log SIEM plus bas |
| `siemExtensionFields` | Attributs additionnels à joindre au log pour ce consommateur, au-delà du socle obligatoire |

### Création/modification d'une fiche d'intégration — contrôle à deux jetons

La fiche d'intégration est elle-même un contrôle de sécurité (un mapping malveillant pourrait
élever silencieusement les droits d'un client) — sa création ne peut donc pas être un
endpoint ouvert. Décision :

- Seul un membre d'un groupe AD DS dédié, Tier 0/Control Plane, peut faire aboutir la création
  ou la modification d'une fiche. L'appartenance à ce groupe exige elle-même une double
  approbation IIQ (hors périmètre coreapi — coreapi ne fait qu'en dépendre).
- L'action côté API n'est valide que si **deux jetons distincts** accompagnent la requête :
  1. Un jeton IIQ portant la demande approuvée, avec en données le contenu proposé de la
     fiche.
  2. Un jeton d'approbation (ex. ServiceNow) attestant qu'un membre du groupe Tier 0 mentionné
     ci-dessus a validé cette fiche précise.
- Cette action emprunte le chemin d'exécution Privilégié déjà défini plus haut (pas un
  mécanisme séparé), et produit un événement d'audit avec son propre `change_type` dans le
  log SIEM (voir schéma plus bas) — cohérent avec le principe "aucune exception à l'audit."

### Liaison entre les deux jetons — hash en V1, certificat en extension future

**Décision** : les deux jetons doivent porter le même contenu de fiche de façon vérifiable,
pour empêcher qu'une approbation valide soit présentée à côté d'un contenu substitué. V1 :
**hash du corps de la fiche**, porté par les deux jetons, comparé par coreapi avant toute
création/modification — c'est un minimum, pas une solution définitive.

Une liaison par certificat (le jeton d'approbation signé directement sur le contenu exact de
la fiche, pas seulement un hash comparé côté coreapi) est une amélioration future plausible,
mais n'est pas un prérequis pour démarrer — comme pour le suivi multi-instance, ne pas
bloquer le développement courant sur cette piste :
- **`main`** — développement actif, liaison par hash uniquement
- **`feature/integration-record-cert-binding`** — branche créée le 2026-07-18 pour la version
  à liaison par certificat, à reprendre si le hash s'avère insuffisant en pratique

## Trois chemins d'exécution technique (câblage DI)

Correction en session : réduire à deux chemins techniques (Standard / Control Plane) était
une régression — la distinction plan/chemin ne collapse pas les trois chemins d'accès
demandés dès le départ (utilisateur, applicatif, privilégié) en deux. Ce sont trois chemins
distincts, chacun avec son propre credential technique, même si l'autorisation qui les
précède peut partager le même mécanisme (claim signé) pour les deux premiers.

- **Utilisateur / Standard** (Spec 4) : le `LdapDirectoryConnection` Singleton actuel
  (`builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>()` dans
  `Program.cs`) — une connexion, un compte de service dédié à la gestion des utilisateurs,
  à moindre privilège, liée pour toute la durée de vie de l'app.
- **Applicatif** (Spec 5, comptes de service — l'équivalent de l'ancien Tier 1) : son **propre**
  Singleton, avec son **propre** compte de service dédié — distinct du compte Standard. Même
  raison que pour la séparation des chemins en amont : un compte de service est une cible de
  plus grande valeur qu'un utilisateur ordinaire, la compromission d'un chemin ne doit pas
  automatiquement donner accès à l'autre (séparation des tâches, confinement du rayon
  d'impact). Pas de JIT/Vault nécessaire ici — toujours du Data/Workload, pas Control Plane.
- **Privilégié / Control Plane** (Spec 7) : **pas de Singleton**. Une connexion par requête,
  jamais réutilisée, qui déclenche Vault au début de l'opération, s'authentifie avec le
  credential retourné, exécute une seule action, puis relâche/signale explicitement la fin à
  Vault.

Trois enregistrements distincts dans le câblage DI, trois comptes de service AD distincts —
pas une variation de deux.

## Stratégie de credential — les trois comptes, pas seulement Control Plane

*Définitions génériques (gMSA, zero standing access, secretless, isolation EC2 vs Fargate) :
voir `.wip/kb` `zero-standing-access` et `ecs-fargate-task-isolation`. Ce qui suit est le
choix propre à coreapi.*

Correction en session : gMSA n'est pas une mesure réservée au chemin Control Plane — le
principe "zéro secret statique" s'applique aux **trois** comptes de service que coreapi
utilise (Standard, Applicatif, Privilégié). Aucun des trois ne doit avoir de mot de passe
classique connu d'un humain.

### Cible de déploiement (critère ajouté à Spec 9)

**AWS ECS Fargate**, pas EC2 launch type. Vu que coreapi est une passerelle privilégiée vers
AD, l'isolation micro-VM par task compte pour les trois chemins, pas seulement Control Plane.
Fargate supporte gMSA nativement depuis mars 2024, donc ce choix ne sacrifie rien côté gMSA.

### gMSA comme base pour les trois comptes

Les trois comptes de service (Standard, Applicatif, Privilégié) sont des gMSA — aucun des
trois n'a de mot de passe classique connu d'un humain.

### Ce qui reste spécifique au Control Plane : le traitement JIT en plus du gMSA

Le gMSA seul (rotation automatique ~30 jours, compte activé en continu) suffit pour Standard
et Applicatif, cohérent avec leurs connexions Singleton à durée de vie longue. Le Control
Plane a une exigence supplémentaire — **"no standing access"**, pas juste "pas de mot de passe
statique" : même le gMSA du compte Control Plane ne doit pas être utilisable en permanence.
Décision : combiner deux mécanismes plutôt que choisir entre eux, parce qu'ensemble ils
ferment les angles morts que chacun laisse seul :
1. **Vault (externe, hors périmètre coreapi)** orchestre une rotation JIT supplémentaire —
   valide seulement pendant la fenêtre de bail active
2. **Le compte AD Control Plane est désactivé par défaut**, activé automatiquement uniquement
   pour la fenêtre d'usage — même si un mot de passe passé fuitait, le compte reste
   inutilisable tant qu'il n'est pas explicitement (r)activé

coreapi ne fait que **déclencher l'événement** vers Vault (et signaler la fin d'usage) pour ce
troisième compte — il n'implémente ni la rotation, ni l'activation/désactivation, ni la
logique de bail. Ce même compte d'amorçage que `credentials-fetcher` utilise pour lire
`msDS-ManagedPassword` est soumis à la même règle : jamais de standing access, pour aucun des
trois comptes.

### Cible secretless

En utilisant le ticket Kerberos déjà obtenu par `credentials-fetcher` plutôt qu'un mot de
passe explicite, `LdapDirectoryConnection` doit basculer de `AuthType.Basic` (comportement
actuel) vers `AuthType.Negotiate` — pour **les trois** chemins, pas seulement Control Plane.
Implication code, pas seulement infra — à traiter au moment de l'implémentation de chaque
chemin, pas avant.

## Couche réseau — combinée, pas alternative

Pour le Control Plane spécifiquement, deux mécanismes combinés (pas l'un ou l'autre) :
- **Segmentation réseau** — subnet dédié, listener/ALB interne séparé, Security Group
  n'autorisant que ce chemin
- **mTLS avec certificats clients par plan** — plus robuste que l'IP source seule en
  environnement cloud (NAT, IP dynamiques)

Combinés à la couche d'autorisation (claim JWT + vérification API-à-API pour Control Plane),
la politique devient : `(preuve d'autorisation valide) ET (chemin réseau attendu)` — jamais
l'un sans l'autre.

## Audit — non négociable, sans exception

Chaque action dans l'API doit être traçable et auditable. Non-répudiation, non-shadow-IT,
rien de non-gouverné. S'applique à toutes les actions, sur les trois plans, indépendamment du
mécanisme de vérification utilisé (claim seul ou vérification API-à-API). Recoupe `CODE-03`
de l'audit Codex — reste à concevoir (piste : Spec 8 "hooks métier", ou une spec dédiée,
à trancher).

## Traçabilité et détection de réutilisation de jeton

Pas un choix entre "référence d'approbation par action" et "confiance du client entier" — ce
sont deux besoins différents qui n'ont pas à partager le même mécanisme :

- **Traçabilité universelle** (obligatoire, sans exception, tous les plans) : chaque action
  produit un log structuré, propre à coreapi, expédié vers le SIEM. Le SIEM gère sa propre
  rétention longue durée — coreapi ne garde sa copie locale que le temps de validité du jeton
  (`jti`), uniquement pour la détection de réutilisation en temps réel, pas pour l'audit
  long terme (ça, c'est le rôle du SIEM une fois le log expédié).
- **Détection de réutilisation** : pas un blocage dur par défaut sur la simple réutilisation
  d'un jeton (un client qui crée 200 groupes dans le même job légitime réutilise forcément le
  même jeton pour ses 200 appels — bloquer ça casserait un usage normal). Le log propre à
  coreapi permet de savoir si un `jti` a déjà été vu, et sert de base à une détection de
  motifs incohérents (même jeton depuis deux sources incompatibles, volume anormal) plutôt
  qu'un simple compteur "vu une fois = mort."
- **Consommation d'une référence d'approbation précise** (ticket ServiceNow, requête IIQ) :
  confirmé hors périmètre de coreapi — décision business/gouvernance, pas technique. coreapi
  ne vérifie ni ne consomme cette référence lui-même.

### Format du log SIEM (résout "format exact du log — `CODE-03`")

*Définition générique des modèles Splunk CIM utilisés ci-dessous : voir `.wip/kb`
`splunk-cim-data-models` (statut `suspect` — à revalider contre l'instance Splunk cible avant
implémentation).*

Cible : Splunk, aligné sur le Common Information Model (CIM) pour que les détections et
rapports de conformité déjà construits côté SOC reconnaissent nos événements sans parsing
custom. Deux niveaux, pour maîtriser le volume :

**1. Socle CIM obligatoire, toujours envoyé** — nos actions sont fondamentalement des
événements de type *Change* (CRUD sur un objet AD) :

| Champ | Contenu coreapi |
|---|---|
| `action` | create / update / delete / read |
| `change_type` | Type d'objet affecté (user / group / ou / serviceAccount / acl / integrationRecord) |
| `object` | `objectGUID` de la cible — **PII : GUID par défaut**, DN uniquement si la fiche d'intégration du consommateur l'autorise en opt-in (`piiDisclosureLevel`) |
| `object_category`, `object_id`, `object_path` | Catégorie de l'objet, identifiant, chemin OU conteneur (pas le DN complet) |
| `user` | Identité de l'appelant (`clientId`/`cid` du JWT — au sens CIM, `user` désigne qui agit, pas la cible) |
| `result`, `status` | Succès/échec + catégorie d'erreur (jamais le message d'exception brut) |
| `vendor_product` | `"coreapi"` |
| `dest` | Domaine/DC cible |
| `src` | IP source de l'appelant |

Enveloppe d'indexation Splunk : `sourcetype` = `coreapi:audit:change` (et `coreapi:audit:auth`
pour les événements de validation de jeton, alignés sur le data model CIM Authentication).

**2. Extension coreapi, configurable par fiche d'intégration** — hors CIM standard mais
coexiste sans problème à côté du socle : `coreapi_jti`, `coreapi_correlationId`,
`coreapi_executionPath`, `coreapi_approvalReference`, `coreapi_scope`, plus
`siemExtensionFields` déclarés dans la fiche d'intégration du consommateur. Le socle reste
toujours envoyé ; c'est cette extension qui varie en volume selon les exigences du
consommateur, pas le socle.

### Instance unique vs multi-instance — deux branches séparées

coreapi tournera probablement en plusieurs instances en production (Fargate, disponibilité).
Si le suivi de `jti` vit uniquement en mémoire locale du processus, une instance ne sait pas
qu'une autre a déjà vu le même jeton — angle mort de détection entre instances. Décision :
ne pas trancher maintenant, développer les deux pistes séparément :
- **`main`** — développement actif, suivi local, hypothèse instance unique
- **`feature/multi-instance-support`** — branche créée le 2026-07-18 pour la version avec un
  composant de suivi partagé (type Redis/ElastiCache) entre instances, à reprendre quand la
  charge le justifiera

## Ce qui n'est PAS dans le périmètre de coreapi

- Le moteur/l'interface du workflow d'approbation (IIQ, ServiceNow ou autre gèrent ça)
- L'implémentation de Vault (rotation, gestion de bail, moteur de secrets AD) — présupposé
  exister, coreapi ne fait que déclencher
- La décision métier de qui a le droit de demander quoi — c'est une décision de gouvernance,
  pas technique
- La consommation/validité d'une référence d'approbation précise (ticket ServiceNow, requête
  IIQ) — confirmé hors périmètre, coreapi ne fait que journaliser, pas vérifier

## Ouvert, non résolu

- **Spec 6 (groupes/OU) et la sensibilité de l'objet cible** — un groupe de distribution
  ordinaire et un groupe de sécurité privilégié ne devraient probablement pas être traités de
  façon identique même s'ils sont tous deux "des groupes." Piste évoquée : la sensibilité de
  l'objet cible, pas le type d'objet, détermine le traitement — à confirmer.
- **Liaison cryptographique des deux jetons de création/modification d'une fiche
  d'intégration** — comment garantir que le jeton d'approbation (ServiceNow) porte bien sur le
  contenu exact du jeton de demande (IIQ) et pas sur un contenu substitué. Piste : hash du
  corps de la fiche porté par les deux jetons, comparé par coreapi.
- **Vérification API-à-API pour le Control Plane** — protocole exact vers quelle API côté
  gouvernance, pas encore défini (et pas urgent : aucun code n'atteint ce chemin aujourd'hui)
