# Guide d'implémentation

Ce guide renvoie au [`README.md`](../../README.md) racine pour les commandes exactes (build, tests, provisionnement du DC de test) — elles ne sont pas dupliquées ici pour éviter toute divergence entre deux copies. Ce document ajoute le contexte que le README racine ne couvre pas.

## Construire et tester

Voir [`README.md`](../../README.md) racine, sections « Build », « Unit tests », « Integration tests ». En bref :

```bash
dotnet build
dotnet test --filter "Category=Unit"        # 94 cas, aucune dépendance externe
dotnet test --filter "Category=Integration"  # 5 cas, nécessite un DC réel — voir Spec 0
```

Il n'existe **aucune CI** exécutant ces commandes automatiquement à ce jour (voir [EN-03](../specifications/enablers/en-03-ci-quality-gates.md)) — chaque exécution est manuelle, sur le poste du développeur.

## Structure du code (`src/CoreApi/`)

| Dossier | Contenu | Règle |
|---|---|---|
| `Controllers/` | Couche HTTP uniquement | Pas de logique métier |
| `Services/` | Un service par type d'objet AD | Logique métier + confinement `BaseDn` |
| `Infrastructure/` | Connexion LDAP, encodage anti-injection, autorisation par scope | Pas de logique métier |
| `Models/` | DTOs de requête/réponse | Liste fermée de champs, pas de mass-assignment |
| `Hooks/` | Logique métier transverse | Vide aujourd'hui (`.gitkeep` seul) — voir [Spec 8](../specifications/functional/spec-8-business-logic-hooks.md) |

## Conventions à respecter pour toute nouvelle capacité

- Toute nouvelle ressource AD suit le même schéma de scope `coreapi.ad.<tier>.<resource>.<verb>` que `users` (Spec 4) — voir [`../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md`](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md) avant d'ajouter un tier autre que T2.
- Tout accès LDAP passe par `IDirectoryConnection`/`LdapFilterEncoder`/`LdapDnEncoder` existants — jamais de concaténation de chaîne dans un filtre ou un DN.
- Toute capacité de démonstration doit respecter la contrainte non négociable : **CoreAPI reste un resource server, il n'émet jamais de token** — voir [EN-04](../specifications/enablers/en-04-secure-demo.md).
- Catégorisation des tests par trait xUnit : `[Trait("Category", "Unit")]` pour un test sans dépendance AD, `[Trait("Category", "Integration")]` pour un test nécessitant un DC réel. Un test placé dans `CoreApi.IntegrationTests` sans le trait `Integration` est une erreur de configuration.

## Critères d'évaluation (barre de qualité visée)

*Migré depuis `.wip/docs/coreapi.md`. Ce sont des critères de qualité cibles, pas une affirmation que chaque point est déjà atteint pour toutes les specs — voir [`../assurance/verification-matrix.md`](../assurance/verification-matrix.md) pour l'état réellement vérifié par spec.*

#### Correctness

- Les opérations LDAP sont vérifiées contre une **vraie instance AD DS** — pas de mock de la couche LDAP dans les tests d'intégration (un mock qui diverge du comportement réel a déjà causé des incidents ailleurs).
- Les lectures/écritures d'ACL, une fois Spec 7 construite, devront être recoupées avec la sortie PowerShell `Get-Acl`/`Set-Acl` sur le même objet.
- Toutes les réponses d'erreur REST respectent RFC 7807 (Problem Details) — **déjà conforme** (`ProblemDetailsExceptionHandler`).
- La validation JWT rejette explicitement : token expiré, mauvaise audience, mauvais issuer, signature altérée, `alg: none` — **déjà conforme et testé** (8 cas, `JwtTokenValidationTests`).

**Security**
- Zéro credential en code source ou en sortie de log.
- LDAPS imposé dans toutes les configurations non-développement — **déjà conforme** (garde-fou `Program.cs`, non testé automatiquement — voir dette Spec 2).
- Tous les filtres de recherche LDAP construits sans concaténation de chaîne — **déjà conforme** (`LdapFilterEncoder`).
- Algorithme JWT explicitement en liste blanche ; `alg: none` rejeté au niveau middleware — **déjà conforme et testé**.
- Permissions AD du compte de service documentées et scoped au minimum requis — à vérifier manuellement avant que Spec 9 (déploiement) ne soit livrée.

**Reliability**
- Toute opération AD a un timeout explicite configuré — **déjà conforme** (`DirectoryConnectionOptions.TimeoutSeconds`).
- Un échec de connexion LDAP remonte en `503 Service Unavailable` avec un en-tête `Retry-After`, jamais un `500` brut — **déjà conforme** (`ProblemDetailsExceptionHandler`).
- `GET /health` rapporte la connectivité AD comme un sous-check nommé, distinct de la simple vivacité du process — **non conforme aujourd'hui**, voir [EN-09](../specifications/enablers/en-09-observability-and-audit.md).

**API contract**
- Le document OpenAPI est auto-généré depuis le code (Swashbuckle), jamais maintenu à la main.
- Tous les endpoints versionnés sous `/v1/` — **déjà conforme**.
- Chaque action de contrôleur documente `/// <summary>` et `[ProducesResponseType]` pour chaque code de statut retourné — **non conforme aujourd'hui**, à construire au fil des Specs 4–10 (voir [Spec 10](../specifications/functional/spec-10-openapi-and-api-experience.md)).
- Swagger UI activé et utilisable comme outil de test interactif, dans tous les environnements — **contredit par la dette de sécurité R1** de la revue du 2026-07-19 : Swagger doit au contraire être gardé par environnement avant toute démo externe (voir [EN-04](../specifications/enablers/en-04-secure-demo.md)). Ce critère du document source est donc **volontairement non repris tel quel** — la revue de sécurité prévaut.

**Test coverage**
- Les tests d'intégration des Specs 2–7 tournent contre un DC réel ; mocker `IDirectoryConnection` est interdit dans les tests d'intégration.
- Un test qui passe contre un mock mais pas contre une vraie instance AD est considéré comme un test en échec.

**Observability**
- Chaque requête produit une entrée de log JSON structurée, avec un identifiant de corrélation traçable entre APIs appelantes — **non conforme aujourd'hui**, voir [SEC-03](../specifications/security/sec-03-audit-correlation-non-repudiation.md).
- Aucune PII dans les logs au niveau par défaut — DN, noms d'affichage, mots de passe ne doivent jamais apparaître en `Information` ou en dessous.

**Build quality**
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — **déjà conforme**, zéro avertissement de compilation à la fusion.
- Types de référence nullable activés (`<Nullable>enable</Nullable>`).
- Toutes les dépendances NuGet figées via `packages.lock.json` — **déjà conforme**.

## Definition of Done (par spec)

Une spec n'est considérée complète que si, cumulativement :
- tout le nouveau code compile sans avertissement (`dotnet build -warnaserror`) ;
- les tests unitaires passent (`dotnet test --filter Category=Unit`) ;
- les tests d'intégration passent contre un DC réel, pas un mock ;
- le document OpenAPI a été régénéré ;
- aucun secret n'apparaît dans les fichiers indexés (vérifié par revue du diff avant commit) ;
- toute nouvelle action de contrôleur porte `/// <summary>` et `[ProducesResponseType]` pour chaque code de statut retourné (Specs 4–7 et 10) ;
- pour les incréments à risque élevé, la revue indépendante décrite ci-dessous a eu lieu et ses conclusions sont documentées.

## Revue indépendante pour incréments à risque élevé

**Une revue contradictoire indépendante est obligatoire pour les incréments à risque élevé. L'outil ou le modèle de revue n'est pas imposé et devrait, lorsque possible, être différent de celui ayant principalement produit l'implémentation.**

*(Reformulation de la règle initialement écrite comme « revue OpenAI Codex obligatoire » dans `.wip/docs/coreapi.md` — l'obligation porte sur l'indépendance et le caractère contradictoire de la revue, pas sur un outil ou un fournisseur nommé. Ne pas transformer cette règle en exigence produit rattachée à un outil spécifique.)*

Un incrément est considéré à risque élevé — sans jugement d'appréciation nécessaire — s'il remplit **au moins un** de ces critères :
- touche du code de protocole LDAP bas niveau (usage de `LdapConnection` ou `SearchRequest`) ;
- lit ou écrit des structures d'ACL binaires (usage d'`ActiveDirectorySecurity`) ;
- configure ou valide Kerberos / GSSAPI ;
- modifie un `Dockerfile` ou un point d'entrée de conteneur ;
- câble une logique transverse modifiant l'état de plusieurs types d'objets AD simultanément.

Historiquement identifiés comme à risque élevé selon ces critères : Spec 2 (connexion LDAP), Spec 7 (ACL), Spec 8 (hooks transverses), Spec 9 (déploiement/conteneur).

## Où trouver l'état réel de chaque capacité avant d'y toucher

[`../specifications/catalog.yml`](../specifications/catalog.yml) — vérifier le statut avant de supposer qu'une capacité existe.
