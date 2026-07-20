# Exploitation — CoreAPI

## État actuel

**Aucun environnement de production n'existe.** Aucun runbook n'est donc nécessaire aujourd'hui au-delà de la procédure de provisionnement du DC de test/démo, déjà documentée dans [`../specifications/enablers/spec-0-test-demo-ad-infrastructure.md`](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md) et le `README.md` racine.

Ce dossier est un emplacement réservé (`runbooks/` ne contient qu'un `.gitkeep`), à peupler au fur et à mesure que l'exploitation réelle se construit — pas avant.

## Ce qui manque avant qu'un premier runbook ait du sens

- [EN-09](../specifications/enablers/en-09-observability-and-audit.md) — observabilité et audit (aujourd'hui : logs ponctuels, health check générique ne vérifiant pas AD réellement).
- [EN-10](../specifications/enablers/en-10-resilience-readiness-rate-limiting.md) — résilience, readiness, rate limiting.
- Une décision de cible de déploiement ([`../architecture/views/deployment-view.md`](../architecture/views/deployment-view.md)) — on ne documente pas l'exploitation d'un environnement qui n'existe pas encore.

## Ce qui existe déjà et pourrait éclairer un futur runbook

- Le provisionnement du DC de test/démo (Spec 0) suit déjà une procédure répétable et idempotente — voir [`../specifications/enablers/spec-0-test-demo-ad-infrastructure.md`](../specifications/enablers/spec-0-test-demo-ad-infrastructure.md).
- Le health check `/health` existe (générique, ne vérifie pas AD) — `src/CoreApi/Program.cs:43,142`.
