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

Erreur initiale corrigée en session : un plan (où vit l'actif) et un chemin d'accès (comment
on l'atteint/le gouverne) sont deux axes différents — pas la même chose, pas une
correspondance 1-pour-1.

| Plan | Ce qu'il contient | Specs coreapi concernées |
|---|---|---|
| **Data/Workload** | La quasi-totalité de ce que coreapi gère : comptes utilisateurs (Spec 4), comptes de service (Spec 5), groupes/OU ordinaires (Spec 6) — humain ou compte de service, peu importe, ce sont des actifs Data/Workload | Spec 4, 5, 6 |
| **Management** | Les outils de gouvernance eux-mêmes (IIQ, ServiceNow, PAM) — c'est par ce plan que passe la gouvernance des actifs Data/Workload, pas un chemin séparé pour chaque type d'actif | N/A directement — c'est la source d'autorisation, pas une cible AD |
| **Control** | Les systèmes qui contrôlent l'identité et la sécurité elles-mêmes — Domain Admins, Enterprise Admins, Schema Admins, OU Domain Controllers, la structure des ACL elle-même | Spec 7 (ACL), toute action touchant un groupe/objet Tier 0 |

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

Correction en session : gMSA n'est pas une mesure réservée au chemin Control Plane — le
principe "zéro secret statique" s'applique aux **trois** comptes de service que coreapi
utilise (Standard, Applicatif, Privilégié). Aucun des trois ne doit avoir de mot de passe
classique connu d'un humain.

### Cible de déploiement (critère ajouté à Spec 9)

**AWS ECS Fargate**, pas EC2 launch type. Raison : sur EC2, les tasks ECS partagent le même
noyau hôte — "containers are not a security boundary" selon la documentation AWS elle-même,
un conteneur compromis peut potentiellement atteindre les données d'autres tasks sur le même
hôte. Sur Fargate, chaque task a sa propre micro-VM — frontière d'isolation réelle. Vu que
coreapi est une passerelle privilégiée vers AD, cette isolation compte pour les trois chemins,
pas seulement Control Plane. Fargate supporte gMSA nativement depuis mars 2024
(`credentials-fetcher`), donc ce choix ne sacrifie rien côté gMSA.

### gMSA comme base pour les trois comptes

Les trois comptes de service (Standard, Applicatif, Privilégié) sont des **gMSA** (Group
Managed Service Account) — mot de passe géré et tourné automatiquement par AD, jamais connu
d'un humain, pour aucun des trois. `credentials-fetcher` (daemon AWS) les récupère via LDAPS
et produit un **ticket Kerberos**, pas un mot de passe brut, mis à disposition du conteneur.

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

Si le code utilise le ticket Kerberos déjà obtenu par `credentials-fetcher` plutôt qu'un mot
de passe explicite, `LdapDirectoryConnection` doit basculer de `AuthType.Basic` (bind avec
mot de passe explicite, comportement actuel) vers `AuthType.Negotiate` — pour **les trois**
chemins, pas seulement Control Plane. Le processus coreapi ne manipule alors jamais de mot de
passe en clair, à aucun moment, pour aucun des trois comptes. Implication code, pas seulement
infra — à traiter au moment de l'implémentation de chaque chemin, pas avant.

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
- **Forme exacte du claim d'autorisation dans le JWT** pour le niveau "confiance du client"
  (Option A) — quel nom de claim, quelles valeurs possibles, qui l'émet. Distinct de la
  traçabilité par `jti` (déjà réglée ci-dessus).
- **Format exact du log propre à coreapi** envoyé au SIEM — schéma, champs obligatoires,
  corrélation avec l'identité JWT (`CODE-03` de l'audit Codex)
- **Vérification API-à-API pour le Control Plane** — protocole exact vers quelle API côté
  gouvernance, pas encore défini (et pas urgent : aucun code n'atteint ce chemin aujourd'hui)
