# Décisions Log — CoreAPI

*Migré depuis `.claude/memory/decisions-log.md` (conservé tel quel, non supprimé) et complété par une entrée pour cet incrément. Voir [`README.md`](README.md) pour la règle : une décision n'entre ici que si elle a réellement été approuvée.*

## 2026-06-16 — Init knowledgebase

**Décision** : Initialiser le repo `knowledgebase` comme KB centralisée ouritres.

**Approche retenue** : git submodule monté à `knowledge-base/` dans les repos consommateurs. Aligné avec le chemin attendu par le skill `maxime-kb` (`knowledge-base/index.md`).

**Structure** :
- `index.md` — index léger, chargé systématiquement
- `active/<thème>/` — fiches actives
- `archived/` — fiches archivées

**Alternatives écartées** : package npm/nuget (overkill), symlink (fragile Windows), KB embarquée dans chaque repo (duplication).

**Approuvé par** : Philippe — 2026-06-16

*(Note : la KB a depuis été relocalisée en `.wip/kb/` — commits `fa4ab10`/`0df1f26` — sans qu'une nouvelle décision formelle ne documente ce changement de chemin. Écart signalé, pas corrigé rétroactivement ici, pour ne pas réécrire l'historique d'une décision déjà approuvée.)*

## 2026-06-16 — Choix de socle technique CoreAPI

*(Décision migrée depuis `.wip/docs/coreapi.project-goal.md`, frontmatter `type: decision`, `validated: 2026-06-16` — jamais entrée dans un journal de décisions jusqu'à cette migration, 2026-07-20.)*

**Décision** : Périmètre et socle technique confirmés pour `coreapi`, la passerelle AD DS partagée pour l'organisation Ouritres.

**Approche retenue** :
- C# / .NET 9 / ASP.NET Core Web API.
- Authentification des appelants par JWT Bearer ; issuer/authority configurable au déploiement, jamais codé en dur pour un fournisseur d'identité donné.
- Déploiement cible : AWS, cible exacte (ECS/EKS/Beanstalk) non tranchée à cette date — Kerberos sur conteneurs Linux nécessitera une configuration additionnelle.
- Bibliothèques clé retenues : `System.DirectoryServices.Protocols` (LDAP brut), `System.DirectoryServices.AccountManagement` (utilisateurs/groupes), `System.Security.AccessControl` (gestion d'ACL), middleware JWT Bearer ASP.NET Core.

**Pourquoi** : AD DS est un système Microsoft-natif ; les bibliothèques .NET de premier niveau gèrent Kerberos et les structures ACL binaires nettement mieux que des alternatives en Python.

**Alternatives écartées** : non documentées dans la source — ce choix de socle a été confirmé directement, pas comparé à des alternatives concurrentes dans le document d'origine.

**Approuvé par** : Philippe — 2026-06-16.

## 2026-07-19/20 — Baseline documentaire `docs/` (EN-01) — statut : proposition en attente de validation

**Ceci n'est pas une décision actée.** Cette entrée existe pour retracer précisément la chronologie et éviter toute confusion entre "le travail a été demandé/produit" et "le résultat a été approuvé" — ce sont deux choses différentes.

**1. Demande initiale** : Philippe a demandé la création d'une baseline documentaire canonique pour CoreAPI (incrément EN-01), avec réconciliation des Specs 0–10 contre le code réel.

**2. Production anticipée de la documentation** : le plan d'approche (liste exacte de fichiers à créer sous `docs/`) a été approuvé par Philippe le 2026-07-19, ce qui a autorisé le *lancement des travaux d'écriture*. **Cette approbation ne portait que sur le plan, pas sur le contenu produit.** Le contenu a ensuite été rédigé par anticipation de cette approbation de plan — pas par une approbation ligne à ligne de son contenu.

**3. Revue corrective** : Philippe a relu une première fois le contenu produit et a demandé des corrections (matrice `.wip/docs/` exhaustive, retrait d'une fausse affirmation d'approbation, modèle d'autorisation consolidé, etc.), appliquées le 2026-07-20. Une seconde relecture a demandé des corrections supplémentaires (statut candidate explicite, périmètre CoreAPI-only, retrait des références normatives à `.wip/docs/`, cohérence SEC-01/SPEC-8), également appliquées le 2026-07-20.

**4. Statut actuel** : le contenu produit sous `docs/` reste **une proposition en attente de revue et de validation explicite de Philippe** — il ne doit pas être cité comme une décision approuvée tant que cette validation n'a pas eu lieu, quel que soit le nombre de passes correctives déjà effectuées.

**Ce qui reste à faire avant qu'une entrée de décision légitime remplace celle-ci** : validation explicite de Philippe sur le contenu produit sous `docs/`. Une fois cette validation obtenue, cette entrée doit être remplacée par une entrée datée de la validation réelle, et `EN-01` peut passer au statut `done` dans `docs/specifications/catalog.yml`.

*(Cette condition est remplie par l'entrée du 2026-07-20 ci-dessous — l'entrée ci-dessus est conservée telle quelle pour l'historique de la chronologie, pas réécrite rétroactivement.)*

## 2026-07-20 — Validation explicite de la baseline documentaire CoreAPI (EN-01)

**Décision** : Philippe valide explicitement le contenu produit sous `docs/` (incrément EN-01) comme **documentation canonique du dépôt et du produit CoreAPI**.

**Portée de cette décision** :
- Limitée au produit et au dépôt **CoreAPI** — architecture technique, spécifications, sécurité, trajectoire de ce service précis.
- **Exclut explicitement** toute validation de la gouvernance ou de la roadmap globale des organisations **`IamPhilG`** et **`OurITRes`** — cette gouvernance de portefeuille reste hors périmètre de cette décision et sera portée séparément par `OurITRes/archi-projects`.

**Ce que cette décision change concrètement** :
- `EN-01` passe du statut `in-progress` au statut `done` dans `docs/specifications/catalog.yml` et dans [`../specifications/enablers/en-01-documentation-baseline.md`](../specifications/enablers/en-01-documentation-baseline.md).
- `docs/README.md` cesse de qualifier `docs/` de « baseline candidate » et le qualifie désormais de documentation canonique de CoreAPI.

**Ce que cette décision ne change pas** :
- `.wip/docs/` (6 fichiers) reste en place, non modifié, non déplacé, non supprimé, non marqué `superseded` — cette requalification reste un incrément séparé, non décidé ici.
- Aucun document de gouvernance ou de roadmap de portefeuille n'est créé ni impliqué par cette décision.

**Approuvé par** : Philippe — 2026-07-20.
