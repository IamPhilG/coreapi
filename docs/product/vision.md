# Vision produit — CoreAPI

*Repris et reformulé à partir du [`README.md`](../../README.md) racine et de `.wip/docs/coreapi.project-goal.md` (source historique de cette réconciliation) — aucune orientation nouvelle n'est introduite ici.*

## Périmètre

Cette vision est celle de **CoreAPI seul**. Elle ne décide pas de la priorité de CoreAPI par rapport aux autres produits/dépôts d'`IamPhilG`/`OurITRes` — cette priorisation inter-produits sera gouvernée par `OurITRes/archi-projects`, pas par ce document.

## Ce que CoreAPI est

CoreAPI est la passerelle REST unique vers Active Directory Domain Services (AD DS) pour l'organisation Ouritres. Les API "topiques" de plus haut niveau (gestion des utilisateurs, gestion des groupes, etc.) délèguent à CoreAPI toutes les opérations Active Directory — elles ne parlent jamais directement LDAP.

## Pourquoi ce découplage

- **Un seul point qui parle LDAP/Kerberos/LDAPS** : les autres services n'ont pas à connaître les subtilités du protocole, de l'encodage des filtres, ou de la gestion des connexions AD.
- **Un seul point qui porte la logique métier transverse** propre à AD (ex. appartenances de groupe par défaut à la création d'un utilisateur).
- **Un seul point qui authentifie et autorise ses appelants**, avec un modèle de scopes explicite (`coreapi.ad.<tier>.<resource>.<verb>`) plutôt que de laisser chaque service topique réinventer son propre contrôle d'accès à AD.

## Ce que CoreAPI n'est pas

- Ce n'est pas un fournisseur d'identité : CoreAPI **valide** des JWT Bearer émis par un Authority externe (aujourd'hui Okta), il n'en émet jamais. Voir [`specifications/functional/spec-3-jwt-authentication.md`](../specifications/functional/spec-3-jwt-authentication.md).
- Ce n'est pas (encore) un service multi-tenant ni multi-instance à état partagé — voir [`specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md`](../specifications/spikes/spike-03-multi-instance-shared-jti-tracking.md).
- Ce n'est pas un service de gestion de groupes, de comptes de service ou d'ACL aujourd'hui — ces capacités sont planifiées (Specs 5 à 7), pas construites. Voir [`capability-map.md`](capability-map.md).

## Utilisateur cible

Aujourd'hui : les équipes internes qui construisent des API topiques ayant besoin de lire/écrire des utilisateurs standards (Tier 2) dans AD DS, via un contrat REST stable et un modèle d'autorisation par scope.

## Pour en savoir plus

- Capacités livrées vs planifiées : [`capability-map.md`](capability-map.md)
- Trajectoire : [`roadmap.md`](roadmap.md)
- Architecture complète : [`../architecture/coreapi-sad.md`](../architecture/coreapi-sad.md)
