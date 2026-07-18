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

## Les trois plans (EAM) — classification par objet, pas par Spec

*Définition générique du modèle EAM : voir `.wip/kb` `enterprise-access-model`.*

Erreur initiale corrigée en session : un plan (où vit l'actif) et un chemin d'accès (comment
on l'atteint/le gouverne) sont deux axes différents — pas la même chose, pas une
correspondance 1-pour-1.

Erreur suivante, corrigée en relecture le 2026-07-18 : un plan ne se déduit pas non plus d'une
Spec coreapi prise dans son ensemble ("Spec 4 = Data/Workload"). AD DS est lui-même un système
d'identité, structurellement adjacent au Control Plane — administrer ses objets, même des
comptes utilisateurs ordinaires, n'est pas une opération Data/Workload (qui désigne la donnée
métier/applicative, pas l'infrastructure d'identité qui la protège). Le mécanisme de
classification réel — cascade de tiers par objet AD, indépendante du plan/tier de
l'application appelante — est développé dans
[ad-ds-governance-model.md](ad-ds-governance-model.md). Ce document-ci ne classe plus les
Specs par plan EAM ; il couvre le chemin d'exécution, le credential, le réseau et l'audit.

Point qui reste valide malgré la correction : la gouvernance d'un objet AD (quel que soit son
tier) n'a pas une source unique — voir section suivante.

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

**Correction (relecture du 2026-07-18)** : PAM n'est pas un niveau — c'est un intermédiaire de
contrôle (voir `ad-ds-governance-model.md`), il ne peut donc pas apparaître comme une valeur
de la colonne "Niveau" au même titre qu'un tier. Corrigé ci-dessous. Autre correction : le
niveau ne se déduit plus d'une liste de Specs ("Specs 4, 5, 6") — une Spec n'est pas classée
globalement par tier, c'est chaque cible/geste qui l'est (voir `ad-ds-governance-model.md`).

| Niveau | Mécanisme de vérification | Justification |
|---|---|---|
| Tier 2 — opérations déléguées sur des identités/objets standards | **Claim signé dans le JWT** — coreapi vérifie la signature et la présence du claim d'autorisation, ne valide pas le processus métier derrière | Le coût d'une vérification API-à-API systématique dépasse le bénéfice pour ce volume d'appels routiniers |
| Tier 1 — opérations sur des identités et actifs classifiés Tier 1, notamment certains comptes de service | **Non tranché** — claim JWT avec exigences renforcées et/ou vérification API-à-API selon la sensibilité du geste, à finaliser avant que Spec 5 touche un cas réel de Tier 1 | À décider, pas à laisser tomber implicitement entre Tier 2 et Tier 0 |
| Tier 0 / opérations Control Plane (Spec 7 et au-delà, ACL, cibles Tier 0) | **Vérification API-à-API en direct**, coreapi vers l'API de la source d'autorisation ou le système PAM concerné selon le flux | L'enjeu justifie la complexité additionnelle — c'est précisément le principe de proportionnalité de la sécurité, pas une négligence pour les autres niveaux |

## Claim de confiance client et fiche d'intégration

**Décision (résout le point "forme du claim JWT — Option A")** : pas de nouveau claim
personnalisé. Réutilisation du claim `scope`/`scp` OAuth2 standard, déjà présent dans le
pipeline de validation Spec 3 (confirmé sur un jeton Okta réel en session : `"scp":
["coreapi.access"]`).

### Taxonomie des scopes — nommage par tier, construction incrémentale

Risque identifié : une combinatoire (verbe × ressource × tier × classe de privilège) explose
vite si on tente de l'énumérer d'un coup. Décision : ne pas construire toute la taxonomie
maintenant — définir une **convention de nommage** stable, et n'instancier que les scopes
réellement nécessaires, spec par spec, branche par branche, au fur et à mesure du besoin.

**Convention validée (relecture du 2026-07-18)** : `coreapi.ad.<tier>.<ressource>.<verbe>`

- `<tier>` — le tier de la cible selon la cascade de classification (`t0`, `t1`, `t2`) — voir
  [ad-ds-governance-model.md](ad-ds-governance-model.md)
- `<ressource>` — le type d'objet concerné (`users`, `groups`, `ous`, `service-accounts`,
  `acl`, etc.)
- `<verbe>` — un des cinq verbes retenus : `read` (Get/List), `create`, `update`, `delete`,
  `audit` (accès à la trace d'audit de la ressource, distinct de la lecture de son état
  courant). Les verbes expriment directement la capacité — pas de segment supplémentaire type
  "lifecycle" : ça évite de classer artificiellement `read` et `audit` comme des opérations
  Joiner-Mover-Leaver alors qu'elles ne le sont pas nécessairement.

Le tier de la cible remplace le plan EAM dans le nom du scope : c'est la classification de
l'**objet AD** qui gouverne le scope, pas le plan EAM de l'application appelante — les deux
restent des axes indépendants (voir `ad-ds-governance-model.md`), mais seul le premier
s'exprime dans le nom du scope ; le second vit dans la fiche d'intégration du client.

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

**Classe de privilège déterminée par geste, pas par ressource entière** (correction en
relecture) : pour Spec 4 (`users`, Tier 2), `create`/`update`/`delete` sont des opérations
d'administration privilégiée Tier 2 — elles modifient l'état de l'identité. Seul `read` peut
relever d'une consultation standard/non privilégiée, et seulement pour un sous-ensemble
d'attributs non sensibles — la restriction d'attributs pour `read` n'est pas encore
implémentée aujourd'hui (`UserDto` expose un ensemble fixe, voir brique 5/6 de
`ad-ds-governance-model.md`).

**Scopes concrets validés (Spec 4, ressource `users`, Tier 2)** :
`coreapi.ad.t2.users.read`, `coreapi.ad.t2.users.create`, `coreapi.ad.t2.users.update`,
`coreapi.ad.t2.users.delete`, `coreapi.ad.t2.users.audit`.

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
- Cette action emprunte le profil d'exécution `control-plane-operations` déjà défini plus bas
  (pas un mécanisme séparé), et produit un événement d'audit avec son propre `change_type`
  dans le log SIEM (voir schéma plus bas) — cohérent avec le principe "aucune exception à
  l'audit."

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

## Trois profils d'exécution internes (câblage DI)

Correction en session : réduire à deux profils (Standard / Control Plane) était une
régression — la distinction plan/chemin ne collapse pas les trois profils demandés dès le
départ (utilisateur, applicatif, privilégié) en deux. Ce sont trois profils distincts, chacun
avec son propre credential technique, même si l'autorisation qui les précède peut partager le
même mécanisme (claim signé) pour les deux premiers.

**Renommage (relecture du 2026-07-18)** : "Standard / Applicatif / Privilégié" collisionnait
avec le vocabulaire EAM des chemins d'accès (User/Application/Privileged Access), alors que ce
sont des profils techniques internes (credential/DI/réseau), pas une classification EAM. Noms
validés :

- **`user-identity-operations`** (Spec 4) : le `LdapDirectoryConnection` Singleton actuel
  (`builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>()` dans
  `Program.cs`) — une connexion, un compte de service dédié à la gestion des utilisateurs,
  à moindre privilège, liée pour toute la durée de vie de l'app.
- **`service-identity-operations`** (Spec 5, comptes de service classés Tier 1 dans le cas
  d'usage considéré) : son **propre** Singleton, avec son **propre** compte de service dédié —
  distinct du profil précédent. Même raison que pour la séparation des profils en amont : un
  compte de service est une cible de plus grande valeur qu'un utilisateur ordinaire, la
  compromission d'un profil ne doit pas automatiquement donner accès à l'autre (séparation des
  tâches, confinement du rayon d'impact). Pas de JIT/Vault nécessaire ici, **pour les comptes
  de service qui restent Tier 1** — voir la correction ci-dessous.
- **`control-plane-operations`** (Spec 7) : **pas de Singleton**. Une connexion par requête,
  jamais réutilisée, qui déclenche Vault au début de l'opération, s'authentifie avec le
  credential retourné, exécute une seule action, puis relâche/signale explicitement la fin à
  Vault.

**Correction (relecture du 2026-07-18)** : "l'équivalent de l'ancien Tier 1" appliqué
globalement à Spec 5 ci-dessus était une généralisation excessive. Un compte de service n'est
pas Tier 1 par nature — sa classification dépend de ce qu'il contrôle, exactement comme pour
tout autre objet (cascade de tiers, `ad-ds-governance-model.md`) : Tier 2 s'il ne contrôle que
des ressources Tier 2, Tier 1 s'il contrôle des workloads/serveurs Tier 1, Tier 0 s'il peut
compromettre AD ou un actif Control Plane. Le routage vers `service-identity-operations` doit
donc dépendre de la classification de la cible et du **pouvoir maximal accordé** à ce compte de
service précis (voir "Classification par pouvoir maximal" dans `ad-ds-governance-model.md`),
pas uniquement de sa classe technique "compte de service." Un compte de service capable de
toucher du Tier 0 n'a pas sa place dans ce profil — il relève de `control-plane-operations`.

Trois enregistrements distincts dans le câblage DI, trois comptes de service AD distincts —
pas une variation de deux.

## Stratégie de credential — les trois comptes, pas seulement Control Plane

*Définitions génériques (gMSA, zero standing access, secretless, isolation EC2 vs Fargate) :
voir `.wip/kb` `zero-standing-access` et `ecs-fargate-task-isolation`. Ce qui suit est le
choix propre à coreapi.*

Correction en session : gMSA n'est pas une mesure réservée au profil `control-plane-operations`
— le principe "zéro secret statique" s'applique aux **trois** comptes de service que coreapi
utilise (`user-identity-operations`, `service-identity-operations`, `control-plane-operations`).
Aucun des trois ne doit avoir de mot de passe classique connu d'un humain.

### Cible de déploiement (critère ajouté à Spec 9)

**AWS ECS Fargate**, pas EC2 launch type. Vu que coreapi est une passerelle privilégiée vers
AD, l'isolation micro-VM par task compte pour les trois profils, pas seulement
`control-plane-operations`.

**Correction (relecture du 2026-07-18, vérifiée contre la doc AWS officielle)** : le support
gMSA sur Fargate n'est **pas** générique — c'est spécifiquement le mode *domainless gMSA pour
conteneurs Linux* (depuis mars 2024). coreapi doit donc tourner en conteneurs Linux, pas
Windows, pour bénéficier de ce chemin — à confirmer explicitement comme hypothèse de
déploiement si ce n'est pas déjà acquis ailleurs (rien d'autre dans le code n'exige Windows :
LDAP/Kerberos via `System.DirectoryServices.Protocols` fonctionne sous Linux). Voir aussi
la correction "Cible secretless" plus bas — ce mode n'est pas entièrement secretless.

### gMSA comme base pour les trois comptes

Les trois comptes de service (`user-identity-operations`, `service-identity-operations`,
`control-plane-operations`) sont des gMSA — aucun des trois n'a de mot de passe classique
connu d'un humain.

### Ce qui reste spécifique à `control-plane-operations` : le traitement JIT en plus du gMSA

Le gMSA seul (rotation automatique ~30 jours, compte activé en continu) suffit pour
`user-identity-operations` et `service-identity-operations`, cohérent avec leurs connexions
Singleton à durée de vie longue. `control-plane-operations` a une exigence supplémentaire —
**"no standing access"**, pas juste "pas de mot de passe statique" : même le gMSA de ce
profil ne doit pas être utilisable en permanence.

**Correction (relecture du 2026-07-18, vérifiée)** : la section ci-dessous présentait
"Vault orchestre une rotation JIT supplémentaire" du mot de passe gMSA comme une décision
technique acquise. Ce n'est pas fondé. Un gMSA n'a pas de mot de passe réglable par un système
externe : `msDS-ManagedPassword` est calculé et tourné uniquement par AD lui-même : les
principals autorisés le **récupèrent** (`PrincipalsAllowedToRetrieveManagedPassword`), ils ne
le **définissent** pas. Le moteur AD secrets engine de Vault fait tourner des mots de passe de
comptes de service classiques par écriture LDAP (mécanisme check-out/check-in) — un mécanisme
incompatible avec un gMSA ; aucune documentation consultée ne montre de prise en charge
spécifique. Le mécanisme de "no standing access" pour `control-plane-operations` reste donc
**à concevoir**, pas acquis. Pistes à évaluer, sans en présupposer une :
- Contrôle temporaire de l'appartenance à `PrincipalsAllowedToRetrieveManagedPassword` — qui a
  le droit de récupérer le mot de passe géré à un instant donné, pas rotation du mot de passe
  lui-même
- Activation/désactivation du compte AD Control Plane via `userAccountControl` — seul élément
  de la version précédente de cette section qui reste valide, indépendant du mécanisme Vault
- Contrôle temporaire de l'accès au secret de bootstrap (le credential statique de
  `credentials-fetcher` en AWS Secrets Manager, voir correction gMSA/Fargate plus haut) plutôt
  que du gMSA lui-même
- Autorisation PAM/JIT en amont du segment coreapi (voir le modèle multi-segments,
  `ad-ds-governance-model.md`)

Ce qui reste valide sans changement : **le compte AD Control Plane désactivé par défaut**,
activé automatiquement uniquement pour la fenêtre d'usage — même si un mot de passe passé
fuitait, le compte reste inutilisable tant qu'il n'est pas explicitement (r)activé. coreapi ne
ferait que **déclencher l'événement** d'activation/désactivation vers le système qui l'orchestre
(Vault ou autre, à confirmer) — il n'implémenterait ni la rotation, ni l'activation/
désactivation elle-même, ni la logique de bail.

**Correction précédente (relecture du 2026-07-18)**, toujours valide indépendamment de ce qui
précède : le compte d'amorçage utilisé par `credentials-fetcher` pour lire
`msDS-ManagedPassword` n'est *pas* soumis à une rotation JIT — en mode *domainless gMSA* (le
seul supporté sur Fargate, voir plus haut), `credentials-fetcher` s'appuie sur un identifiant
AD **statique** (utilisateur + mot de passe + domaine) stocké dans AWS Secrets Manager : un
credential permanent, protégé par IAM (accès au secret scoped au rôle d'exécution de la task),
pas par un mécanisme "zéro standing access." Cible secretless plus bas à corriger dans le même
sens.

### Cible secretless

En utilisant le ticket Kerberos déjà obtenu par `credentials-fetcher` plutôt qu'un mot de
passe explicite, `LdapDirectoryConnection` doit basculer de `AuthType.Basic` (comportement
actuel) vers `AuthType.Negotiate` — pour **les trois** profils, pas seulement
`control-plane-operations`. Implication code, pas seulement infra — à traiter au moment de
l'implémentation de chaque profil, pas avant.

**Précision (relecture du 2026-07-18)** : "secretless" s'applique au **processus coreapi**
(il ne manipule jamais de mot de passe en clair) — pas à la chaîne complète. Le mode
domainless gMSA repose sur un secret AD statique dans AWS Secrets Manager, en amont de
coreapi (voir correction ci-dessus). Ne pas présenter cette chaîne comme entièrement
secretless sans avoir décrit et sécurisé ce secret de démarrage — accès scoped par IAM,
rotation à définir (hors périmètre de ce document, à couvrir dans Spec 9).

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
- **Classification de coreapi par pouvoir maximal, pas par opération courante** — si une même
  instance/credential peut toucher des cibles Tier 0 (même occasionnellement), elle doit être
  protégée comme un actif Tier 0 en permanence, pas seulement au moment où elle traite une
  telle requête. Question ouverte : est-ce que ça pousse vers des déploiements coreapi
  séparés par tier (`coreapi-t2`/`coreapi-t1`/`coreapi-t0`), au-delà de la séparation actuelle
  par comptes de service/DI au sein d'un même processus ? Pas tranché — implique un coût
  d'infra/ops significatif si oui.
- **Mécanisme de vérification pour Tier 1** — le tableau "Mécanisme de vérification" ne
  tranche que Tier 2 (claim JWT) et Tier 0/Control Plane (API-à-API). Tier 1 (comptes de
  service et actifs Tier 1, profil `service-identity-operations`) n'a pas de mécanisme défini
  — à trancher avant que Spec 5 touche un cas réel de Tier 1.
- **Mécanisme de "no standing access" pour `control-plane-operations`** — l'ancienne décision
  ("Vault fait tourner le mot de passe gMSA en JIT") s'est révélée techniquement infondée à la
  relecture (un gMSA n'a pas de mot de passe réglable en externe). Reste à concevoir :
  contrôle temporaire de `PrincipalsAllowedToRetrieveManagedPassword`, contrôle temporaire de
  l'accès au secret de bootstrap, ou autorisation PAM/JIT en amont — voir la correction dans
  "Ce qui reste spécifique à `control-plane-operations`."
