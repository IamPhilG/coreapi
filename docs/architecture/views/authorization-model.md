# Modèle d'autorisation et de gouvernance AD DS — CoreAPI

*Document architectural consolidé, créé le 2026-07-20 (incrément EN-01). Il rassemble sans perte le contenu substantiel de deux documents de conception antérieurs — `.wip/docs/architecture/ad-ds-governance-model.md` et `.wip/docs/architecture/authorization-and-access-model.md`, tous deux issus d'une session de conception du 2026-07-18. Ces deux sources restent en place sous `.wip/docs/` (non modifiées, non marquées `superseded` à ce stade) ; ce document-ci est la référence canonique pour toute nouvelle lecture.*

*Chaque composant ci-dessous porte un état réel explicite : **implémenté**, **partiel**, **décidé**, **proposé**, ou **non implémenté**. Un statut n'est jamais déduit d'une intention — il est vérifié contre le code, les tests ou une décision réellement actée.*

---

## 1. Principe fondateur

CoreAPI est un point d'application de la politique (*policy enforcement point*), pas un moteur de décision métier. Il n'implémente jamais :

- le workflow d'approbation lui-même (qui approuve quoi) — vit dans les outils de gouvernance externes (IIQ, ServiceNow, ou autre selon le flux) ;
- la logique métier de décision de sécurité — le métier décide des besoins de sécurité, CoreAPI applique cette décision, il ne la prend pas ;
- l'intégration profonde avec un coffre-fort de secrets (ex. HashiCorp Vault) — présupposé exister hors périmètre, CoreAPI n'implémente que le **mécanisme de déclenchement**.

Ce que CoreAPI fait systématiquement, sans exception, pour chaque action :
1. vérifie qu'une preuve d'autorisation valide accompagne la requête ;
2. exécute l'action avec le niveau de privilège technique approprié ;
3. laisse une trace complète et auditable.

**État réel** : le principe lui-même est une posture de conception, pas un composant vérifiable isolément — mais son application se lit dans chaque section ci-dessous. La partie « vérifie une preuve d'autorisation » est **implémentée et vérifiée** pour le scope OAuth (§8) ; les parties « niveau de privilège approprié » et « trace complète » restent **partielles** (voir §9 et §15).

---

## 2. Classification — les sept briques du modèle de gouvernance

Chaque requête s'évalue selon sept axes indépendants. Ce ne sont pas des alternatives : une même requête traverse les sept, chacun avec son propre état d'implémentation.

| # | Brique | Ce qui existe | Ce qui manque | État réel |
|---|---|---|---|---|
| 1 | **Plan/tier de la cible** | Cascade de classification actée : Tier 0 ? → sinon Tier 2 ? → sinon Tier 1 ? → sinon défaut Tier 0 (fail-secure). Une classe résolue par exception explicite : compte utilisateur standard = Tier 2 (Spec 4). | Aucun mécanisme en code ne détermine le tier d'un objet à l'exécution. Aucune règle écrite pour les autres classes d'objets (groupes, comptes de service, ordinateurs, GPO, ACL/délégations, OU). Pas de référentiel des OU/groupes marquant un objet Tier 0. | **Décidé** (cascade) pour le principe ; **non implémenté** en code, sauf le cas particulier Tier 2/`users` |
| 2 | **Plan/tier de l'intermédiaire d'exécution** | Principe que c'est un axe séparé de la cible (une app peut être Management Plane tout en manipulant une cible Tier 2). Règle d'alerte sur l'élévation : une app touchant du Control Plane devient elle-même Control Plane. Principe multi-segment (§6). | Aucun champ dans la fiche d'intégration (§10) ne capture le plan/tier de l'application cliente elle-même. Aucune vérification technique n'empêche qu'une app enregistrée "Tier 2 App Access uniquement" reçoive un scope touchant une cible Control Plane. | **À construire** |
| 3 | **Chemin d'accès** | CoreAPI n'accepte que `client_credentials` — jamais de flux délégué, jamais d'identité humaine en bout de chaîne. La **modalité d'accès** du segment CoreAPI (application → AD) est donc toujours Application Access. | Aucune vérification technique de cette modalité au runtime (c'est une propriété du flux OAuth choisi, pas un contrôle actif). Confirmation manquante que `control-plane-operations` (Spec 7) reste Application Access renforcé, ou constitue le segment aval d'un Privileged Access Path amont nécessitant une vérification API-à-API vers un PAM. | **Spécifié** pour l'essentiel (sur le seul segment CoreAPI), **partiel** pour le reste |
| 4 | **Classe d'objet** | Spec 4 traite `user` (compte standard) par décision explicite, au cas par cas. | Aucun inventaire des classes AD que CoreAPI touchera (computer, group, service account traditionnel vs sMSA vs gMSA, OU, conteneur, GPO, contact, objets de confiance, objets sites/sous-réseaux/réplication, objets schéma/configuration). Aucune distinction catégorie métier ↔ classe technique AD. Aucun mécanisme de découverte du schéma de la forêt. | **À construire** |
| 5 | **Geste et droit élémentaire** | Les quatre verbes CRUD (read/create/update/delete) sont implémentés et protégés par scope sur `UsersController` (Spec 4). | La décomposition en droits AD élémentaires (permissions sur l'objet, permissions sur un attribut précis, droits étendus — reset password, unlock —, écritures validées) n'existe pas. `UpdateAsync` reste une opération générique, pas une collection d'actions élémentaires alignées sur les droits AD réels. Pas de mapping "geste métier → opérations AD élémentaires" (ex. un "Joiner" = create + placement OU + appartenances de groupe initiales — `CreateAsync` ne gère pas l'appartenance de groupe aujourd'hui). | **Implémenté et vérifié** pour les 4 verbes CRUD de base ; **non implémenté** pour la décomposition fine |
| 6 | **Portée** | Un contrôle domaine/sous-arbre — `EnsureWithinConfiguredBaseDn` refuse toute opération hors du `BaseDn` configuré (`UserService.cs`). | Toute granularité plus fine : portée par OU spécifique, par objet individuel, par attribut, par property set, ou par droit étendu précis. Le contrôle actuel est binaire (dans le périmètre configuré ou non), pas hiérarchique. | **Implémenté et vérifié** pour le confinement structurel `BaseDn` ; **non implémenté** au-delà |
| 7 | **Conditions d'exécution** | Validation stricte du JWT (signature/issuer/audience/algorithme) — implémentée et testée. Détection de réutilisation par `jti`, format de log SIEM, contrôle à deux jetons pour la fiche d'intégration — tous **décidés** en conception (§9, §11, §15). | Élévation JIT par geste, consommation d'une approbation par action (hors périmètre CoreAPI par décision explicite — vit dans la gouvernance externe), séparation des tâches appliquée en code (hors périmètre, décision de gouvernance), contrainte de poste/PAW d'origine, mécanisme de compensation/retour arrière en cas d'échec partiel. | **Partiel** — la validation JWT est acquise, le reste est décidé sans être codé |

### Plans ≠ chemins d'accès, et PAM n'est ni l'un ni l'autre

Les **plans** (Control / Management / Data-Workload, modèle EAM) décrivent *où vit l'actif et son niveau de contrôle*. Les **chemins d'accès** (User Access / Application Access / Privileged Access) décrivent *comment on l'atteint*. Ce sont deux classifications différentes, pas une correspondance 1-pour-1.

**PAM (ex. CyberArk) n'est ni un quatrième plan, ni le nom d'un chemin.** C'est un **intermédiaire de sécurité** qui contrôle un Privileged Access Path — un point de passage obligé (checkpoint/broker) sur ce chemin, pas une catégorie de classification en soi (voir §6).

### Correction actée : un plan ne se déduit pas d'une Spec entière

AD DS est lui-même un système d'identité, structurellement adjacent au Control Plane — administrer ses objets, même des comptes utilisateurs ordinaires, n'est pas une opération Data/Workload. Le mécanisme de classification réel est une cascade de tiers **par objet AD**, indépendante du plan/tier de l'application appelante (brique 1).

### Classification par pouvoir maximal, pas par opération courante

Une application (ou une instance CoreAPI) doit être protégée selon le **pouvoir maximal qu'elle peut exercer**, pas selon ce qu'une requête donnée est en train de faire. Si une même instance/credential CoreAPI peut, même occasionnellement, toucher une cible Tier 0, sa compromission donne potentiellement accès au Tier 0 — elle doit donc être classée et protégée comme un actif Tier 0 en permanence.

**Question ouverte, non tranchée** : cette séparation par credential/DI au sein d'un même processus (§12) suffit-elle, ou faut-il des **déploiements CoreAPI physiquement séparés par tier** (`coreapi-t2` ne pouvant pas charger le code/credential du profil `control-plane-operations`) ? Coût d'infra/ops non négligeable si oui — voir §17.

---

## 3. Access modality vs Privilege class

Deux propriétés indépendantes, à ne jamais fusionner en un seul champ :

- **Access modality** (comment CoreAPI s'authentifie) : toujours *Application Access* — une identité technique/machine, jamais une identité humaine. Ne change jamais, pour aucun des trois profils d'exécution (§12).
- **Privilege class** (la sensibilité de ce que ce segment autorise à faire) : varie selon la cible **et selon le geste**. Une identité machine (Application Access) peut parfaitement exécuter une opération privilégiée — c'est précisément le rôle du profil `control-plane-operations` : Application Access dans sa modalité, mais Privileged dans sa classe, avec les garanties additionnelles (JIT, réseau dédié) que ça implique.

**Exemple travaillé — Spec 4 (utilisateurs standards)** : la classe de privilège se détermine **par geste**, pas une fois pour toutes pour la ressource `users`.

| Axe | `read` | `create` / `update` / `delete` |
|---|---|---|
| Access modality | Application Access | Application Access |
| Privilege class | Standard/non-privilégié (sous réserve d'une restriction d'attributs non encore faite) | **Privileged administration — Tier 2** |
| Cible | Tier 2 | Tier 2 |
| Geste | Consultation d'identité | Identity Lifecycle (Joiner-Mover-Leaver) — étiquette descriptive, absente du nom du scope |

**État réel** : distinction **décidée** et cohérente avec le code existant (`ScopePolicies` protège les 4 verbes indépendamment) mais **non implémentée** comme restriction d'attributs pour `read` (`UserDto` expose un ensemble fixe de champs, pas un sous-ensemble selon la sensibilité).

---

## 4. PAM et modèle multi-segment

Une opération peut traverser plusieurs segments, avec un chemin différent à chaque segment. Exemple de référence : un opérateur humain accède à une application d'administration par un **Privileged Access Path contrôlé par PAM** (segment 1) ; cette application exécute ensuite la modification dans AD par un **Application Access Path**, avec une identité technique (segment 2).

**Position de CoreAPI dans la chaîne** : CoreAPI n'occupe jamais que le **dernier segment** (application → AD), toujours avec une identité technique (gMSA visé, §13). CoreAPI ne voit jamais directement un segment amont impliquant PAM ou un opérateur humain — il ne reçoit qu'un JWT dont il doit faire confiance qu'un processus de gouvernance en amont a déjà validé les segments précédents. CoreAPI applique la politique, il ne négocie pas avec les intermédiaires qui l'ont précédé.

Conséquence pour l'audit (§15) : la trace produite par CoreAPI ne couvre que son propre segment. Une corrélation de bout en bout à travers les segments amont (PAM, IIQ, ServiceNow) suppose un identifiant de corrélation partagé et propagé par ces systèmes — **non confirmé aujourd'hui** (§17).

**État réel** : modèle conceptuel **décidé** ; aucune implémentation de corrélation multi-segment, aucune représentation d'un éventuel intermédiaire PAM dans la fiche d'intégration (§10) ou le log (§15) — **non implémenté**.

---

## 5. Pipeline d'autorisation (CoreAPI)

Onze étapes, reformulées depuis le principe fondateur (§1) et étendues à la classification d'objet :

1. Identifier l'acteur (l'application appelante) et son rôle — **amorcé** (JWT + `claimMapping` visé, §10)
2. Identifier précisément l'objet cible — **implémenté** (route + paramètres du contrôleur)
3. Déterminer le plan, le tier, la portée et la classification de la cible — **à construire** (brique 1, 4)
4. Traduire le geste métier demandé en opérations AD élémentaires — **à construire** (brique 5)
5. Évaluer la règle de gouvernance correspondante — **à construire**
6. Vérifier les conditions d'accès et les éventuelles approbations — **partiellement amorcé** (validation JWT stricte implémentée ; contrôle à deux jetons et JIT non implémentés)
7. Utiliser une identité d'exécution disposant uniquement des droits nécessaires — **partiellement amorcé** (un seul profil `user-identity-operations` existe en code, avec un compte de service classique, pas encore gMSA — §12/§13)
8. Exécuter l'opération — **implémenté**
9. Vérifier le résultat — **implicitement amorcé**, via les exceptions `NotFoundException`/`ConflictException`
10. Produire une trace complète et exploitable — **schéma défini (§15), non branché** en code
11. Déclencher, si nécessaire, une procédure de compensation/retour arrière — **à construire**

**État réel consolidé** : étapes 1, 6, 7, 9, 10 amorcées partiellement ; étapes 2 et 8 implémentées ; étapes 3, 4, 5, 11 entièrement à construire.

---

## 6. Gouvernance à sources multiples

Plusieurs sources de preuve d'autorisation coexistent selon le cas d'usage — pas de passage obligé par un seul outil :

- **IIQ** — pour les flux qui exigent une gouvernance humaine explicite ;
- **Ticket ServiceNow** — validation suffisante pour certains flux (ex. une application qui a un besoin légitime et documenté de créer des comptes de service) ;
- **Scope pré-autorisé / self-service** — pour des cas d'usage comme la création de comptes de service ou la gestion de groupes "à la volée," où exiger un passage systématique par le Management Plane à chaque appel n'est pas réaliste opérationnellement.

CoreAPI n'a pas besoin de savoir parler à IIQ, ServiceNow, ou tout futur outil de gouvernance. Il a besoin d'une façon **standard** de recevoir et vérifier une assertion d'autorisation, quelle que soit la source qui l'a produite.

**État réel** : principe **décidé** ; le mécanisme standard retenu est le claim `scope`/`scp` OAuth2 (§8) — **implémenté** pour Tier 2.

---

## 7. Mécanisme de vérification — proportionnel à l'enjeu

**Principe Zero Trust visé, non atteint en V1** : dans l'idéal, chaque appel serait vérifié en direct (API-à-API, CoreAPI vers l'API de la source d'autorisation). Décision explicite : ne rien coder maintenant pour les tiers non encore atteints par le code.

| Niveau | Mécanisme de vérification | État réel |
|---|---|---|
| Tier 2 — opérations déléguées sur identités/objets standards | **Claim signé dans le JWT** — CoreAPI vérifie la signature et la présence du claim d'autorisation, ne valide pas le processus métier derrière | **Implémenté et vérifié** (`ScopePolicies`, 12 tests d'autorisation bout-en-bout) |
| Tier 1 — identités et actifs classifiés Tier 1, notamment certains comptes de service | **Non tranché** — claim JWT renforcé et/ou vérification API-à-API selon la sensibilité du geste, à finaliser avant que Spec 5 touche un cas réel de Tier 1 | **Non implémenté**, décision explicitement différée |
| Tier 0 / opérations Control Plane (Spec 7 et au-delà) | **Vérification API-à-API en direct**, CoreAPI vers l'API de la source d'autorisation ou le système PAM concerné | **Non implémenté** — aucun code n'atteint ce chemin aujourd'hui |

---

## 8. Taxonomie des scopes

**Convention validée** : `coreapi.ad.<tier>.<ressource>.<verbe>`

- `<tier>` — le tier de la cible selon la cascade de classification (`t0`, `t1`, `t2`) ;
- `<ressource>` — le type d'objet concerné (`users`, `groups`, `ous`, `service-accounts`, `acl`, etc.) ;
- `<verbe>` — un des cinq verbes retenus : `read`, `create`, `update`, `delete`, `audit` (accès à la trace d'audit de la ressource, distinct de la lecture de son état courant).

Le tier de la cible remplace le plan EAM dans le nom du scope : c'est la classification de l'**objet AD** qui gouverne le scope, pas le plan EAM de l'application appelante — les deux restent des axes indépendants (brique 2), mais seul le premier s'exprime dans le nom du scope ; le second vivrait dans la fiche d'intégration du client (§10, non implémenté).

**Limite technique à connaître** : les IdP usuels (Okta, Entra) ne font pas de correspondance par préfixe/wildcard sur les scopes — chaque scope est une chaîne exacte, accordée individuellement à un client. Des policies "grossières" basées sur un préfixe resteraient à implémenter côté CoreAPI lui-même, si le besoin apparaît — non fait aujourd'hui.

**Séparation des tâches (SoD)** : CoreAPI vérifie chaque scope **indépendamment, sans inférence** — détenir `create` n'implique jamais `delete`, ni aucun autre verbe. La décision de quelles combinaisons de scopes peuvent être accordées ensemble à un même client est une politique de gouvernance (IIQ), hors périmètre technique de CoreAPI.

**Scopes concrets validés (Spec 4, ressource `users`, Tier 2)** : `coreapi.ad.t2.users.read`, `coreapi.ad.t2.users.create`, `coreapi.ad.t2.users.update`, `coreapi.ad.t2.users.delete`, `coreapi.ad.t2.users.audit`.

**État réel** : `read`/`create`/`update`/`delete` — **implémentés et vérifiés**. `audit` — **décidé** dans la convention, **non implémenté** (aucune ressource d'audit exposée par l'API à ce jour).

---

## 9. Fiche d'intégration client

L'IdP émet le claim au moment de l'enregistrement du client appelant, mais CoreAPI opère avec plusieurs consommateurs, potentiellement plusieurs IdP, qui ne nomment pas forcément leurs scopes de la même façon. D'où la **fiche d'intégration** : un registre, un enregistrement par consommateur, qui traduit les claims entrants vers l'ensemble d'attributs canoniques de CoreAPI.

| Champ | Rôle |
|---|---|
| `appId` | Identifiant unique du consommateur (`client_id`), assigné à l'onboarding, immuable |
| `status` | Active / Inactive / Décommissionnée — statut technique de l'habilitation à appeler CoreAPI |
| `claimMapping` | Règles traduisant le(s) claim(s) entrant(s) de ce consommateur vers les valeurs canoniques |
| `piiDisclosureLevel` | GUID (défaut) ou DN (opt-in) — voir §15 |
| `siemExtensionFields` | Attributs additionnels à joindre au log pour ce consommateur |

**Champs manquants identifiés par la brique 2** (§2) : plan/tier de l'application cliente elle-même, indicateur `pamRequired` pour les capacités qui l'exigeraient — à réconcilier avant implémentation.

**État réel** : schéma **proposé** ; aucun registre, aucune table, aucun code correspondant — **non implémenté**.

---

## 10. Création/modification d'une fiche d'intégration — contrôle à deux jetons

La fiche d'intégration est elle-même un contrôle de sécurité (un mapping malveillant pourrait élever silencieusement les droits d'un client) — sa création ne peut donc pas être un endpoint ouvert.

- Seul un membre d'un groupe AD DS dédié, Tier 0/Control Plane, peut faire aboutir la création ou la modification d'une fiche. L'appartenance à ce groupe exige elle-même une double approbation IIQ (hors périmètre CoreAPI).
- L'action côté API n'est valide que si **deux jetons distincts** accompagnent la requête :
  1. un jeton IIQ portant la demande approuvée, avec en données le contenu proposé de la fiche ;
  2. un jeton d'approbation (ex. ServiceNow) attestant qu'un membre du groupe Tier 0 a validé cette fiche précise.
- Cette action emprunterait le profil d'exécution `control-plane-operations` (§12), et produirait un événement d'audit avec son propre `change_type` (§15).

### Liaison entre les deux jetons — hash en V1, certificat en extension future

Les deux jetons doivent porter le même contenu de fiche de façon vérifiable, pour empêcher qu'une approbation valide soit présentée à côté d'un contenu substitué.

- **V1 retenue** : hash du corps de la fiche, porté par les deux jetons, comparé par CoreAPI avant toute création/modification — un minimum, pas une solution définitive.
- **Extension future envisagée** : liaison par certificat (le jeton d'approbation signé directement sur le contenu exact de la fiche). Une branche marqueur, `feature/integration-record-cert-binding`, a été créée le 2026-07-18 pour cette piste — **elle ne contient aucun travail réel** (diff contre `main` = suppressions uniquement, base de fusion antérieure aux PR #4/#5). Voir [`../../specifications/spikes/spike-04-integration-record-cert-binding.md`](../../specifications/spikes/spike-04-integration-record-cert-binding.md).

**État réel** : mécanisme entier (fiche + deux jetons + liaison hash) **décidé**, **non implémenté** — aucun code correspondant. Extension certificat : **spike non commencé**, branche vide.

---

## 11. Profils d'exécution internes (câblage DI)

Trois profils distincts, chacun avec son propre credential technique — pas deux, malgré une tentation de collapse en session :

- **`user-identity-operations`** (Spec 4) : le `LdapDirectoryConnection` Singleton actuel (`builder.Services.AddSingleton<IDirectoryConnection, LdapDirectoryConnection>()` dans `Program.cs`) — une connexion, un compte de service dédié à la gestion des utilisateurs, à moindre privilège, liée pour toute la durée de vie de l'app.
- **`service-identity-operations`** (Spec 5) : son **propre** Singleton, avec son **propre** compte de service dédié — distinct du profil précédent. Un compte de service est une cible de plus grande valeur qu'un utilisateur ordinaire ; la compromission d'un profil ne doit pas automatiquement donner accès à l'autre. **Correction actée** : un compte de service n'est pas Tier 1 par nature — sa classification dépend de ce qu'il contrôle (brique 1) ; un compte de service capable de toucher du Tier 0 relèverait de `control-plane-operations`, pas de ce profil.
- **`control-plane-operations`** (Spec 7) : **pas de Singleton**. Une connexion par requête, jamais réutilisée, qui déclencherait un mécanisme JIT au début de l'opération, s'authentifierait avec le credential retourné, exécuterait une seule action, puis relâcherait/signalerait explicitement la fin.

Trois enregistrements distincts dans le câblage DI, trois comptes de service AD distincts — pas une variation de deux.

**État réel** : `user-identity-operations` — **implémenté et vérifié** (code + tests), mais avec un compte de service classique, pas encore gMSA (§13). `service-identity-operations` et `control-plane-operations` — **proposés**, **non implémentés** (aucun code, aucun câblage DI correspondant ; `Spec 5` et `Spec 7` sont `not-started`).

---

## 12. Stratégie de credential — les trois comptes, pas seulement Control Plane

Le principe "zéro secret statique" viserait les **trois** comptes de service, pas seulement `control-plane-operations`. Aucun des trois ne devrait avoir de mot de passe classique connu d'un humain.

### Cible de déploiement (critère ajouté à Spec 9 dans le document source)

Le document source affirme **« AWS ECS Fargate, pas EC2 launch type »** comme cible retenue, avec l'isolation micro-VM par task pour les trois profils. **Note de réconciliation (2026-07-20)** : la revue d'architecture/sécurité du 2026-07-19 (révision 2) a requalifié cette affirmation — un seul document l'affirmait comme décidée, alors que trois autres documents produit disaient "TBD (ECS/EKS/Beanstalk)" et qu'aucune preuve dans le code actuel ne montre que le socle gMSA domainless a été validé en pratique. **ECS Fargate Linux reste donc une cible candidate, conditionnelle à un POC** ([`spike-01-ecs-fargate-linux-viability.md`](../../specifications/spikes/spike-01-ecs-fargate-linux-viability.md)), pas une décision entérinée — voir [`deployment-view.md`](deployment-view.md).

Point technique conservé du document source : le support gMSA sur Fargate n'est pas générique — c'est spécifiquement le mode *domainless gMSA pour conteneurs Linux* (depuis mars 2024). CoreAPI devrait donc tourner en conteneurs Linux, pas Windows, pour bénéficier de ce chemin (rien dans le code n'exige Windows aujourd'hui : LDAP/Kerberos via `System.DirectoryServices.Protocols` fonctionne sous Linux).

### gMSA comme base pour les trois comptes

Les trois comptes de service seraient des gMSA — aucun des trois n'aurait de mot de passe classique connu d'un humain. **État réel : proposé, non implémenté** — le code actuel utilise un compte de service classique (`ServiceAccountUser`/`ServiceAccountPassword`, mot de passe en configuration).

### `control-plane-operations` : le traitement JIT en plus du gMSA

Le gMSA seul (rotation automatique ~30 jours) suffirait pour les deux premiers profils. `control-plane-operations` aurait une exigence supplémentaire — "no standing access" : même le gMSA de ce profil ne devrait pas être utilisable en permanence.

**Correction actée, importante** : une version antérieure de cette section présentait "un coffre-fort de secrets orchestre une rotation JIT du mot de passe gMSA" comme acquis. **Ce n'est pas fondé techniquement** — un gMSA n'a pas de mot de passe réglable par un système externe (`msDS-ManagedPassword` est calculé et tourné uniquement par AD lui-même ; les principals autorisés le *récupèrent*, ils ne le *définissent* pas). Le mécanisme de "no standing access" pour `control-plane-operations` reste donc **à concevoir**, pas acquis. Pistes à évaluer, sans en présupposer une :

- contrôle temporaire de l'appartenance à `PrincipalsAllowedToRetrieveManagedPassword` ;
- activation/désactivation du compte AD Control Plane via `userAccountControl` (compte désactivé par défaut, activé automatiquement pour la fenêtre d'usage — seul élément qui reste valide sans changement) ;
- contrôle temporaire de l'accès au secret de bootstrap (le credential statique de `credentials-fetcher` en gestionnaire de secrets AWS, voir ci-dessous) plutôt que du gMSA lui-même ;
- autorisation PAM/JIT en amont du segment CoreAPI (§4).

Point additionnel conservé : le compte d'amorçage utilisé par `credentials-fetcher` pour lire `msDS-ManagedPassword` n'est **pas** soumis à une rotation JIT — en mode *domainless gMSA*, `credentials-fetcher` s'appuie sur un identifiant AD **statique** (utilisateur + mot de passe + domaine) stocké dans un gestionnaire de secrets AWS : un credential permanent, protégé par IAM, pas par un mécanisme "zéro standing access."

### Cible secretless — et sa limite documentée

En utilisant le ticket Kerberos déjà obtenu par `credentials-fetcher` plutôt qu'un mot de passe explicite, `LdapDirectoryConnection` basculerait de `AuthType.Basic` vers `AuthType.Negotiate` — pour **les trois** profils, pas seulement `control-plane-operations`.

**Précision importante conservée du document source** : "secretless" s'appliquerait au **processus CoreAPI** (il ne manipule jamais de mot de passe en clair) — pas à la chaîne complète. Le mode domainless gMSA repose sur un secret AD statique dans un gestionnaire de secrets AWS, en amont de CoreAPI. Ne pas présenter cette chaîne comme entièrement secretless sans avoir décrit et sécurisé ce secret de démarrage.

**État réel** : cible **proposée**, **non implémentée** — le code actuel utilise `AuthType.Basic`/`AuthType.Negotiate` selon configuration, sans logique "secretless" intentionnelle.

---

## 13. Couche réseau — combinée, pas alternative

Pour le Control Plane spécifiquement, deux mécanismes combinés (pas l'un ou l'autre) seraient requis :
- **Segmentation réseau** — subnet dédié, listener/ALB interne séparé, Security Group n'autorisant que ce chemin ;
- **mTLS avec certificats clients par plan** — plus robuste que l'IP source seule en environnement cloud (NAT, IP dynamiques).

Combinés à la couche d'autorisation (§7), la politique deviendrait : `(preuve d'autorisation valide) ET (chemin réseau attendu)` — jamais l'un sans l'autre.

**État réel** : **proposé**, **non implémenté** — aucun code réseau de ce type n'existe.

---

## 14. Audit — traçabilité et détection de réutilisation de jeton

Chaque action doit être traçable et auditable, sans exception, sur les trois plans.

- **Traçabilité universelle** (obligatoire, sans exception) : chaque action produirait un log structuré, propre à CoreAPI, expédié vers le SIEM. Le SIEM gérerait sa propre rétention longue durée — CoreAPI ne garderait sa copie locale que le temps de validité du jeton (`jti`), uniquement pour la détection de réutilisation en temps réel.
- **Détection de réutilisation** : pas un blocage dur par défaut sur la simple réutilisation d'un jeton (un client qui crée 200 groupes dans le même job légitime réutilise forcément le même jeton). Le log permettrait de savoir si un `jti` a déjà été vu, base d'une détection de motifs incohérents (même jeton depuis deux sources incompatibles, volume anormal).
- **Consommation d'une référence d'approbation précise** (ticket ServiceNow, requête IIQ) : confirmé **hors périmètre** de CoreAPI — décision business/gouvernance, pas technique.

### Format du log SIEM

Cible : Splunk, aligné sur le Common Information Model (CIM). Deux niveaux :

**1. Socle CIM obligatoire, toujours envoyé** — événements de type *Change* (CRUD sur un objet AD) :

| Champ | Contenu CoreAPI |
|---|---|
| `action` | create / update / delete / read |
| `change_type` | Type d'objet affecté (user / group / ou / serviceAccount / acl / integrationRecord) |
| `object` | `objectGUID` de la cible — PII : GUID par défaut, DN uniquement si `piiDisclosureLevel` l'autorise en opt-in |
| `object_category`, `object_id`, `object_path` | Catégorie, identifiant, chemin OU conteneur (pas le DN complet) |
| `user` | Identité de l'appelant (`clientId`/`cid` du JWT) |
| `result`, `status` | Succès/échec + catégorie d'erreur (jamais le message d'exception brut) |
| `vendor_product` | `"coreapi"` |
| `dest` | Domaine/DC cible |
| `src` | IP source de l'appelant |

`sourcetype` = `coreapi:audit:change` (et `coreapi:audit:auth` pour les événements de validation de jeton).

**2. Extension CoreAPI, configurable par fiche d'intégration** : `coreapi_jti`, `coreapi_correlationId`, `coreapi_executionPath`, `coreapi_approvalReference`, `coreapi_scope`, plus les `siemExtensionFields` déclarés par consommateur.

### Instance unique vs multi-instance

Si le suivi de `jti` vit uniquement en mémoire locale du processus, une instance ne sait pas qu'une autre a déjà vu le même jeton. Décision : ne pas trancher maintenant, deux pistes séparées — `main` (suivi local, hypothèse instance unique) et `feature/multi-instance-support` (composant de suivi partagé type Redis/ElastiCache, à reprendre quand la charge le justifiera). **Cette branche ne contient aucun travail réel** (mêmes constats de vacuité que pour `feature/integration-record-cert-binding`, §10) — voir [`spike-03-multi-instance-shared-jti-tracking.md`](../../specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md).

**État réel** : traçabilité universelle et format SIEM — **décidés**, **non implémentés** (seuls des logs `ILogger` ponctuels existent en code). Détection de réutilisation `jti` — **décidée**, **non implémentée**. Suivi multi-instance — **décidé de ne pas trancher maintenant** ; spike non commencé, branche vide.

---

## 15. Ce qui n'est pas dans le périmètre de CoreAPI

- le moteur/l'interface du workflow d'approbation (IIQ, ServiceNow ou autre) ;
- l'implémentation d'un coffre-fort de secrets (rotation, gestion de bail, moteur de secrets AD) — présupposé exister, CoreAPI ne fait que déclencher ;
- la décision métier de qui a le droit de demander quoi — décision de gouvernance, pas technique ;
- la consommation/validité d'une référence d'approbation précise (ticket ServiceNow, requête IIQ) — CoreAPI journalise, ne vérifie pas.

---

## 16. Questions ouvertes, non résolues

Consolidées depuis les deux documents source :

- **Inventaire des classes d'objet AD** — aucun travail commencé, prérequis à toute extension au-delà de Spec 4.
- **Mécanisme de détermination du tier à l'exécution** — cascade actée en principe, aucune implémentation, aucun référentiel.
- **Champ "plan/tier de l'intermédiaire" dans la fiche d'intégration** — absent du schéma actuel (§9).
- **Décomposition des gestes en droits AD élémentaires** — `UpdateAsync` notamment reste une opération non décomposée.
- **Corrélation d'audit inter-segments** — CoreAPI ne journalise que son propre segment ; relier cette trace à un éventuel segment PAM/opérateur amont suppose un identifiant de corrélation partagé et propagé par des systèmes externes, non confirmé.
- **Statut EAM du profil `control-plane-operations`** — Application Access renforcé, ou segment aval d'un Privileged Access Path amont nécessitant une vérification API-à-API vers un PAM ?
- **Déploiements CoreAPI séparés par tier** — la séparation actuelle (comptes de service/DI/réseau distincts au sein d'un même processus) suffit-elle, ou le pouvoir maximal d'une instance capable de toucher du Tier 0 justifie-t-il des déploiements physiquement séparés ? Coût d'infra/ops non négligeable si oui — à trancher, pas à supposer.
- **Sensibilité de l'objet cible pour Spec 6 (groupes/OU)** — un groupe de distribution ordinaire et un groupe de sécurité privilégié ne devraient probablement pas être traités de façon identique même s'ils sont tous deux "des groupes."
- **Liaison cryptographique des deux jetons de la fiche d'intégration** — comment garantir que le jeton d'approbation porte bien sur le contenu exact du jeton de demande (piste hash actée en V1, §10 — la question ouverte porte sur une éventuelle extension par certificat).
- **Vérification API-à-API pour le Control Plane** — protocole exact vers quelle API côté gouvernance, non défini (pas urgent : aucun code n'atteint ce chemin aujourd'hui).
- **Mécanisme de "no standing access" pour `control-plane-operations`** — voir §12, plusieurs pistes non tranchées.
- **Mécanisme de vérification pour Tier 1** — non défini, à trancher avant que Spec 5 touche un cas réel de Tier 1 (§7).

---

## 17. Table récapitulative — état réel de chaque composant

| Composant | État réel | Preuve / raison |
|---|---|---|
| Scope Tier 2 `users` (read/create/update/delete) | **Implémenté et vérifié** | `ScopePolicies.cs`, 12 tests d'autorisation bout-en-bout |
| Scope `.audit` | **Décidé**, non implémenté | Convention validée, aucune ressource d'audit exposée |
| Confinement structurel `BaseDn` | **Implémenté et vérifié** | `EnsureWithinConfiguredBaseDn`, testé y compris astuce de suffixe |
| Cascade de classification par tier (brique 1) | **Décidé**, non implémenté | Aucun mécanisme runtime, sauf le cas `users`=Tier2 codé en dur |
| Classification de l'intermédiaire (brique 2) | **À construire** | Aucun champ, aucune vérification |
| Chemin d'accès — modalité (brique 3) | **Spécifié**, non vérifié activement | `client_credentials` uniquement, propriété du flux OAuth, pas un contrôle actif |
| Classe d'objet — inventaire (brique 4) | **À construire** | Seul `user` traité |
| Décomposition des gestes en droits AD (brique 5) | **Non implémenté** | `UpdateAsync` générique |
| Portée fine (OU/objet/attribut, brique 6) | **Non implémenté** | Contrôle binaire `BaseDn` uniquement |
| Conditions d'exécution — JWT strict (brique 7) | **Implémenté et vérifié** | 8 tests `JwtTokenValidationTests` |
| Conditions d'exécution — JIT/approbation (brique 7) | **Décidé**, non implémenté | — |
| Trois profils d'exécution (DI) | **Partiel** | `user-identity-operations` implémenté (compte classique) ; les deux autres proposés |
| gMSA (trois comptes) | **Proposé**, non implémenté | Code actuel = compte de service classique |
| Cible Fargate domainless gMSA | **Candidate** (reclassé depuis "décidé" par la revue de sécurité 2026-07-19) | Aucune validation technique dans le code ; POC requis (SPIKE-01) |
| JIT `control-plane-operations` | **À concevoir** | Ancienne hypothèse Vault infirmée techniquement |
| Cible secretless | **Proposé**, non implémenté | `AuthType.Basic`/`Negotiate` actuel, sans logique secretless |
| Couche réseau combinée (segmentation + mTLS) | **Proposé**, non implémenté | — |
| Fiche d'intégration client | **Proposé**, non implémenté | Schéma défini, aucun code |
| Contrôle à deux jetons | **Décidé**, non implémenté | — |
| Liaison hash (V1) | **Décidé**, non implémenté | — |
| Liaison certificat (extension) | **Spike non commencé** | `feature/integration-record-cert-binding` vide |
| Traçabilité universelle + format SIEM | **Décidé**, non implémenté | Seuls des logs `ILogger` ponctuels existent |
| Détection de réutilisation `jti` | **Décidé**, non implémenté | — |
| Suivi `jti` multi-instance | **Décidé de différer** | `feature/multi-instance-support` vide, spike non commencé |

---

## Sources et provenance

Ce document consolide, sans les dupliquer intégralement ailleurs, le contenu de conception de :
- `.wip/docs/architecture/ad-ds-governance-model.md` (session de conception du 2026-07-18)
- `.wip/docs/architecture/authorization-and-access-model.md` (session de conception du 2026-07-18)

Les deux fichiers restent en place sous `.wip/docs/`, non modifiés par cet incrément. Ils ne sont pas encore marqués `superseded` — cette requalification interviendra dans un incrément ultérieur, après validation explicite de Philippe sur le contenu de ce document.

## Specs de sécurité qui en découlent

Ce document porte le **modèle**. Les incréments de sécurité à réaliser pour le faire progresser vivent dans :
- [`../../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md`](../../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md) — briques 1, 3, 4, 5, 6 ; fiche d'intégration
- [`../../specifications/security/sec-02-execution-identities-secrets-least-privilege.md`](../../specifications/security/sec-02-execution-identities-secrets-least-privilege.md) — profils d'exécution, credential, secretless
- [`../../specifications/security/sec-03-audit-correlation-non-repudiation.md`](../../specifications/security/sec-03-audit-correlation-non-repudiation.md) — audit, format SIEM, corrélation
- [`../../specifications/security/sec-04-tiers-jit-privileged-paths.md`](../../specifications/security/sec-04-tiers-jit-privileged-paths.md) — tiers, JIT, PAM, multi-segment
- [`../../specifications/security/sec-05-ldap-policy-and-certificates.md`](../../specifications/security/sec-05-ldap-policy-and-certificates.md) — politique LDAP/certificats (largement indépendant du présent modèle, lien via la cible secretless §12)
