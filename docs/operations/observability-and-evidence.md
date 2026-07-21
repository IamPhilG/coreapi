# Observabilité et preuve de tests (COREAPI-04a)

## Ce que fournit COREAPI-04a

- **Traces opérationnelles structurées** (`Microsoft.Extensions.Logging`) : cycle de vie de l'application, événement de requête (méthode, endpoint, statut, durée, `Authenticated`, `SubjectFingerprint`, policy, nombre de résultats, catégorie d'erreur), opérations LDAP (type, host, transport, durée, résultat, code LDAP, pages/résultats), exceptions traitées. Corrélation par `X-Correlation-ID` propagée en scope de log.
- **Preuve technique de campagne de tests** : `tools/run-integration-evidence.ps1` produit un dossier horodaté (transcript, TRX, sortie console, manifeste JSON avec provenance et empreintes SHA-256 des artefacts), plus un artifact CI (TRX + JSON de vulnérabilités).

## Portée — ce qui n'est PAS fourni par cet incrément

- **Ce n'est pas un journal d'audit métier.** Les traces opérationnelles décrivent le comportement technique du service, pas un registre d'actions métier à valeur probante.
- **La capacité `coreapi.ad.t2.users.audit` n'est pas réalisée** ici ; elle reste un incrément séparé (rôle auditeur et production de preuves métier).
- **Pseudonymisation, pas anonymisation.** `SubjectFingerprint`/`ObjectFingerprint` sont des **HMAC-SHA-256** (128 bits / 32 hex) à séparation de domaine (`subject:` / `object:`), stables permettant la corrélation sans journaliser l'identité ou la structure d'annuaire en clair ; ils ne garantissent pas l'irréversibilité face à un attaquant capable d'énumérer les entrées candidates ou détenant la clé. L'identité exacte et sa rétention contrôlée relèvent du futur journal d'audit métier.
  - **Clé** (`Observability:PseudonymizationKey`, ≥ 32 octets) : **obligatoire hors Development/Test** (échec clair au démarrage sinon, sans jamais exposer la clé), stable par environnement, stockée dans le mécanisme de secrets de la plateforme, jamais codée en dur ni journalisée. Une **rotation de clé change toutes les empreintes** et coupe la corrélation avec l'historique.
- **Non-répudiation complète, immutabilité et rétention long terme sont hors périmètre.** Les empreintes d'artefacts rendent une altération détectable localement, mais aucun stockage inviolable ni horodatage tiers n'est fourni.

## Confidentialité des logs

Ne sont jamais journalisés : JWT/token, header `Authorization`, mot de passe, credentials LDAP, clés privées, corps HTTP, DN ou filtre LDAP en clair. Les exceptions annuaire attendues sont expurgées (type + code + catégorie uniquement, jamais `Message`/`ToString`). Seule une exception réellement inattendue conserve sa stack trace interne, à laquelle CoreAPI n'ajoute aucune donnée de requête, token ni credential.
