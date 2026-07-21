using System.Net;
using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Guardrails;

// COREAPI-02 Result 2: minimal, configurable rate limiting. Requests under the limit are not
// throttled and the first request over it returns 429. Enabled by default outside Development, off
// in Development so local work is never throttled. /health is always exempt. Proven end-to-end over
// HTTP, no AD or AWS.
[Trait("Category", "Unit")]
public sealed class RateLimitingTests
{
    // The rate limiter runs before authorization, so an unauthenticated business-route request
    // that is under the limit reaches auth and returns 401, while one over the limit is rejected
    // with 429 first. That makes /v1/users a token-free probe for "is this route limited?".
    private const string BusinessRoute = "/v1/users";
    private const string HealthRoute = "/health";

    // A long window keeps the whole burst inside a single fixed window, so the outcome is
    // deterministic regardless of test timing.
    private static Dictionary<string, string?> Limits(int permitLimit) => new()
    {
        ["RateLimiting:PermitLimit"] = permitLimit.ToString(),
        ["RateLimiting:WindowSeconds"] = "60",
        ["RateLimiting:QueueLimit"] = "0",
    };

    private static async Task<List<HttpStatusCode>> HammerAsync(HttpClient client, string path, int count)
    {
        var statuses = new List<HttpStatusCode>(count);
        for (int i = 0; i < count; i++)
        {
            HttpResponseMessage response = await client.GetAsync(path);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    [Fact]
    public async Task Business_route_under_the_limit_is_not_throttled_then_the_next_request_returns_429()
    {
        // No explicit Enabled flag: this also proves the limiter is on by default in Production.
        using var factory = new GuardrailWebApplicationFactory("Production", Limits(permitLimit: 2));
        HttpClient client = factory.CreateClient();

        List<HttpStatusCode> statuses = await HammerAsync(client, BusinessRoute, count: 3);

        // Under the limit: reached auth (401), not throttled.
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[1]);
        // Over the limit: rejected before auth.
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }

    [Fact]
    public async Task Health_is_never_rate_limited_even_over_the_limit()
    {
        using var factory = new GuardrailWebApplicationFactory("Production", Limits(permitLimit: 2));
        HttpClient client = factory.CreateClient();

        List<HttpStatusCode> statuses = await HammerAsync(client, HealthRoute, count: 5);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task Limiter_is_disabled_by_default_in_Development()
    {
        // Development leaves the limiter off even with a tiny configured limit, so local Swagger
        // and local tests are never throttled: the business route never returns 429.
        using var factory = new GuardrailWebApplicationFactory("Development", Limits(permitLimit: 2));
        HttpClient client = factory.CreateClient();

        List<HttpStatusCode> statuses = await HammerAsync(client, BusinessRoute, count: 5);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }
}
