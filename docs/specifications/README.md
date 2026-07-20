# Spécifications — CoreAPI

Ce dossier contient toutes les spécifications de CoreAPI, réparties en quatre familles strictement séparées :

| Famille | Dossier | Contenu |
|---|---|---|
| **Fonctionnel** | [`functional/`](functional/) | Capacités exposées à un consommateur de l'API (ce que CoreAPI fait pour ses appelants) |
| **Enabler** | [`enablers/`](enablers/) | Fondations techniques et travaux d'infrastructure/outillage, non consommés directement par un appelant de l'API |
| **Sécurité** | [`security/`](security/) | Questions de sécurité transverses non encore résolues (autorisation fine, secrets, audit, tiers privilégiés, politique LDAP) |
| **Spike** | [`spikes/`](spikes/) | Investigations à mener avant de pouvoir décider ou spécifier — pas des engagements |

Le catalogue complet et la traçabilité sont dans [`catalog.yml`](catalog.yml).

## Comment lire un statut

| Statut | Signification |
|---|---|
| `done` | Implémenté **et** couvert par un test qui échouerait si la capacité régressait |
| `partial` | Une partie substantielle existe et fonctionne, mais un écart connu et documenté subsiste |
| `in-progress` | En cours de réalisation active |
| `not-started` | Aucun code correspondant n'existe — recherché explicitement, absence confirmée |
| `planned` | Statut par défaut de toute nouvelle famille (enabler/sécurité/spike) tant qu'aucune preuve d'implémentation n'existe |
| `superseded` | Remplacé par un autre item du catalogue (ex. une branche ou une approche abandonnée) |

**Règle stricte** : un statut `done` ou `partial` doit toujours pointer vers une preuve vérifiable (`acceptance_evidence` dans `catalog.yml` : chemin de test, commit, résultat de commande). Si la preuve n'existe pas, le statut ne peut pas être `done`, quelle que soit l'intention.

## Comment lire `catalog.yml`

Chaque entrée a le schéma suivant :

```yaml
- id: SPEC-4
  title: CRUD utilisateurs et scopes
  type: functional            # functional | enabler | security | spike
  status: done                # done | partial | not-started | planned | superseded
  owner: Philippe
  depends_on: [SPEC-2, SPEC-3]
  blocks: []
  source_requests: [revue-2026-07-19]
  acceptance_evidence:
    - tests/CoreApi.UnitTests/Controllers/UsersControllerAuthorizationTests.cs
    - commit 78a45ac (fusionné main via PR #5)
  last_reviewed: "2026-07-19"
```

- `depends_on` : ce dont cet item a besoin pour avancer.
- `blocks` : ce que cet item empêche d'avancer tant qu'il n'est pas traité.
- `source_requests` : la demande d'origine (issue GitHub, ou `revue-2026-07-19` quand l'item vient directement du backlog de la revue de sécurité, faute d'issue GitHub dédiée).
- `acceptance_evidence` : liste de preuves concrètes, ou `[]` avec une note explicite si aucune preuve n'existe encore (jamais laissé ambigu).

## Ne jamais faire

- Ne jamais marquer `done` une capacité des Specs 5 à 10 sans preuve de code + test.
- Ne jamais présenter les branches `feature/integration-record-cert-binding` ou `feature/multi-instance-support` comme du travail réalisé — voir [`spikes/spike-04-integration-record-cert-binding.md`](spikes/spike-04-integration-record-cert-binding.md) et [`spikes/spike-03-multi-instance-shared-jti-tracking.md`](spikes/spike-03-multi-instance-shared-jti-tracking.md).
- Ne jamais faire émettre un token par CoreAPI dans une spec, même de démonstration — voir [`enablers/en-04-secure-demo.md`](enablers/en-04-secure-demo.md).
