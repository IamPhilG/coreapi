---
name: spec-3-jwt-authentication
description: Architecture & Design Decisions for Spec 3 - JWT Bearer authentication middleware
metadata:
  type: architecture
  spec: 3
  status: done
  complexity: low (no AD DS involvement, not on the complex build gate)
---

# Spec 3 — JWT Bearer Authentication Middleware

## Objective

Validate Bearer tokens presented by caller topic APIs before letting requests reach a
protected controller. coreapi never issues tokens -- it only validates them. `Authority`,
`Audience`, and `Issuer` are configurable per deployment, never hardcoded to a specific
identity provider.

## Architecture overview

```
Caller (topic API)
    ↓ Authorization: Bearer <JWT>
AddJwtBearer (Program.cs)
    ↓
TokenValidationParameters
    - ValidateIssuer / ValidateAudience / ValidateLifetime
    - ValidateIssuerSigningKey + ValidAlgorithms allowlist (defense in depth vs alg:none)
    ↓
Signing key source (branches on environment):
    Development + Jwt:DevSigningKeyPath set  → local static RSA public key (no network call)
    Otherwise                                 → options.Authority → live OIDC discovery
                                                 + JWKS fetch (real IdP: Okta, Entra ID, ...)
    ↓
[Authorize] on BaseApiController → 401 if invalid, request proceeds if valid
```

## Components

### `JwtOptions` (`src/CoreApi/Infrastructure/JwtOptions.cs`)

| Setting | Required | Notes |
|---|---|---|
| `Authority` | Always (fail-fast via `ValidateOnStart()`) | Drives JWKS discovery in non-dev-key mode. Still validated as non-empty even when `DevSigningKeyPath` bypasses the network call, so config drift is caught immediately either way. |
| `Audience` | Always | Must match the `aud` claim exactly. |
| `Issuer` | Always | Must match the `iss` claim exactly. |
| `ValidAlgorithms` | Defaults to `["RS256"]` | Explicit allowlist on top of the library's default `RequireSignedTokens=true` -- both Okta and Entra ID sign with RS256 by default. |
| `DevSigningKeyPath` | Optional, Development-only | Path to a local RSA public key (PEM). Enforced via a `.Validate()` check in `Program.cs` that it's never set outside Development. |

### `tools/DevTokenMinter`

Console tool, generates an RSA-2048 keypair on first run (gitignored: `dev-signing-key.*.pem`),
mints tokens for 6 profiles: `valid`, `expired`, `wrong-audience`, `wrong-issuer`, `unsigned`
(alg: none), `tampered`. Lets Spec 3 be exercised (Swagger Authorize, curl) with zero external
dependency. Usage: `dotnet run -- <profile> [issuer] [audience]` from `tools/DevTokenMinter/`.

### `AuthorizeCheckOperationFilter`

Swashbuckle `IOperationFilter` that adds the Bearer security requirement (padlock icon) only
to operations whose controller/action carries `[Authorize]` (and doesn't carry
`[AllowAnonymous]`) -- so `/health` doesn't misleadingly show as requiring a token.

Note: Microsoft.OpenApi v2 (pulled in by Swashbuckle.AspNetCore 10.x) restructured its
namespaces -- `Microsoft.OpenApi.Models` became `Microsoft.OpenApi`, and
`OpenApiSecurityScheme.Reference` became a dedicated `OpenApiSecuritySchemeReference(id,
document)` type. `OperationFilterContext.Document` provides the `OpenApiDocument` needed to
construct it.

## Verified against a real external IdP (Okta)

The Authority/JWKS-fetch code path (used in every real deployment) is the one piece unit
tests structurally can't cover, since they exercise `DevSigningKeyPath`'s static-key path
instead. It was verified once, live, against a real Okta org:

- **Org:** Okta Integrator Free Plan (`https://integrator-2848612.okta.com`)
- **Authorization Server:** `default` -- ships with **no Access Policy** on the free
  Integrator plan (unlike paid Workforce orgs); one had to be created manually
  (Security → API → Authorization Servers → default → Access Policies) before any
  `client_credentials` token request would succeed.
- **Custom scope:** `coreapi.access` (created under the `default` server's Scopes tab --
  Client Credentials grant can't use OIDC default scopes like `openid`/`profile`, it needs a
  custom one)
- **`Jwt:Authority` / `Jwt:Issuer`:** `https://integrator-2848612.okta.com/oauth2/default`
- **`Jwt:Audience`:** `api://default`
- **Known gotcha:** new Okta Service (API Services) apps require DPoP by default since a
  Feb-2026 Okta release (`invalid_dpop_proof` error). Disabled under the app's General
  Settings ("Require Demonstrating Proof of Possession (DPoP) header") to use plain Bearer
  tokens, matching what this spec implements. DPoP itself (sender-constrained tokens) is not
  implemented -- would be a separate feature if ever required.

**Verification method:** a scratch console app (not committed) used
`ConfigurationManager<OpenIdConnectConfiguration>` with `OpenIdConnectConfigurationRetriever`
-- the same mechanism `AddJwtBearer` uses internally for `options.Authority` -- to fetch live
OIDC discovery + JWKS from the Okta org above, then ran `JwtSecurityTokenHandler.ValidateToken`
against a real `client_credentials`-issued access token using the exact
`TokenValidationParameters` shape `Program.cs` configures. Result: **PASS**, real Okta token
validated end-to-end against live-fetched signing keys.

## Deferred

Accepting tokens from **multiple** trusted issuers simultaneously (e.g. some callers via
Entra ID, others via Okta, on the same running instance) is explicitly out of scope for this
spec -- current design supports one configurable `Authority`/`Issuer` per deployment, not
several at once. Tracked in
[IamPhilG/coreapi#2](https://github.com/IamPhilG/coreapi/issues/2).

## Known gap

No protected controller action exists yet (`BaseApiController` is abstract, 0 concrete
actions until Spec 4), so the literal end-to-end HTTP behavior (`401` without a token, `200`
with one, against a real route) has not been exercised -- only the token-validation logic
itself (unit tests + live Okta check above). Trivial to close once Spec 4 adds real
endpoints.
