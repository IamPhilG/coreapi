# Modèle de menaces (STRIDE) — CoreAPI

*Extrait et actualisé depuis la revue d'architecture/sécurité du 2026-07-19 (révision 2, §4). Le rapport complet, avec preuves détaillées et registre de risques priorisé, reste la référence : [`reviews/2026-07-19-architecture-security-review.md`](reviews/2026-07-19-architecture-security-review.md).*

**Actifs** : mot de passe du compte de service AD, clé de signature JWT/JWKS de l'Authority, données d'annuaire AD (PII : nom, email, manager), disponibilité du DC, intégrité des scopes/policies.

**Acteurs** : Topic API légitime (appelant normal), Topic API compromise, opérateur AWS (test infra), attaquant réseau externe, attaquant interne avec accès réseau au DC de test.

**Frontières de confiance** : Caller↔CoreAPI (JWT) ; CoreAPI↔AD (LDAP/LDAPS, compte de service) ; Opérateur↔AWS (IAM/SSM, test infra uniquement). Détail visuel : [`../architecture/views/context-and-trust-boundaries.md`](../architecture/views/context-and-trust-boundaries.md).

| STRIDE | Scénario | Contrôle existant | Contrôle manquant | Risque résiduel | Spec associée |
|---|---|---|---|---|---|
| Spoofing | Token forgé/altéré | signature RS256 vérifiée, alg allow-list | sender-constraining (mTLS/DPoP) — absent | Faible (accepté pour Tier 2) | [SPEC-3](../specifications/functional/spec-3-jwt-authentication.md) |
| Spoofing | Identité d'exécution implicite (`Negotiate`) non maîtrisée — **ce n'est pas un bind anonyme** | topologie domain-joined attendue par conception | garde-fou explicite absent | Moyen | [SEC-02](../specifications/security/sec-02-execution-identities-secrets-least-privilege.md) |
| Tampering | Injection LDAP/DN via champ utilisateur | `LdapFilterEncoder`/`LdapDnEncoder` RFC 4515/4514, testés | pas de test bout-en-bout via appel contrôleur ; CRLF non testé explicitement | Faible-Moyen | [SPEC-2](../specifications/enablers/spec-2-ldap-connection-layer.md) |
| Tampering | Modification hors du tier autorisé | DTOs à liste fermée de champs | pas d'autorisation par attribut | Faible aujourd'hui | [SEC-01](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md) |
| Repudiation | Absence de traçabilité des décisions d'autorisation | logs `ILogger` ponctuels | pas de log d'audit structuré, pas de SIEM | Moyen-Élevé pour une future prod | [SEC-03](../specifications/security/sec-03-audit-correlation-non-repudiation.md) |
| Information disclosure | Swagger exposé sans garde d'environnement | `ProblemDetailsExceptionHandler` ne fuite pas les messages internes | garde d'environnement absent | Élevé aujourd'hui → Critique si déploiement partagé/public | [SPEC-10](../specifications/functional/spec-10-openapi-and-api-experience.md), [EN-04](../specifications/enablers/en-04-secure-demo.md) |
| Information disclosure | Mot de passe Domain Administrator en clair dans le contenu de la commande SSM | aucun | Secrets Manager/Parameter Store SecureString | Élevé (test infra) | [EN-06](../specifications/enablers/en-06-test-dc-hardening.md) |
| Information disclosure | Simple Bind LDAP non chiffré sur port 389 (seeding démo) | aucun | LDAPS obligatoire | Élevé (test infra) | [EN-06](../specifications/enablers/en-06-test-dc-hardening.md), [SEC-05](../specifications/security/sec-05-ldap-policy-and-certificates.md) |
| Denial of service | Consommation excessive (recherches LDAP coûteuses) | `MaxSearchResults`, pagination interne | aucune limitation de débit HTTP | Élevé | [EN-10](../specifications/enablers/en-10-resilience-readiness-rate-limiting.md) |
| Denial of service | DC de test laissé allumé indéfiniment en mode démo | aucun | arrêt automatique après délai | Moyen | [EN-06](../specifications/enablers/en-06-test-dc-hardening.md) |
| Elevation of privilege | Confusion inter-tier via scope mal formé | test unitaire de matching prouve le rejet | pas de preuve bout-en-bout HTTP | Faible-Moyen (écart de preuve, pas de faille connue) | [SPEC-4](../specifications/functional/spec-4-user-crud-and-scopes.md), [SEC-01](../specifications/security/sec-01-authorization-by-client-ou-object-attribute.md) |
| Elevation of privilege | `ouPath` échappant au périmètre configuré | `EnsureWithinConfiguredBaseDn`, testé y compris astuce de suffixe | — | Faible, bien couvert | [SPEC-4](../specifications/functional/spec-4-user-crud-and-scopes.md) |

Registre de risques priorisé complet (avec niveau de preuve, sévérité et priorité de traitement séparés) : [`reviews/2026-07-19-architecture-security-review.md`](reviews/2026-07-19-architecture-security-review.md) §6.
