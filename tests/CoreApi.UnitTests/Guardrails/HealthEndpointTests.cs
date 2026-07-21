using System.Net;
using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Guardrails;

// COREAPI-02 Result 5: the application boots through its full HTTP pipeline with no real Domain
// Controller and no AWS, and a safe, existing, anonymous route (/health) answers in every
// environment. This is a real HTTP request through the pipeline, not a direct controller call.
[Trait("Category", "Unit")]
public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Health_endpoint_answers_without_a_domain_controller(string environment)
    {
        using var factory = new GuardrailWebApplicationFactory(environment);

        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
