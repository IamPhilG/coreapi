using System.Net;
using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Guardrails;

// COREAPI-02 Result 1: Swagger/OpenAPI is served in Development but the endpoints are not even
// mapped in Production. Proven end-to-end over the real HTTP pipeline, with no AD or AWS.
[Trait("Category", "Unit")]
public sealed class SwaggerEnvironmentTests
{
    [Theory]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/index.html")]
    public async Task Swagger_is_reachable_in_Development(string path)
    {
        using var factory = new GuardrailWebApplicationFactory("Development");

        HttpResponseMessage response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/index.html")]
    public async Task Swagger_is_not_served_in_Production(string path)
    {
        using var factory = new GuardrailWebApplicationFactory("Production");

        HttpResponseMessage response = await factory.CreateClient().GetAsync(path);

        // The endpoint is absent, not hidden: routing returns 404 rather than a 200 or a redirect.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
