using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Observability;

/// <summary>Static guarantees on the committed evidence tooling: the integration-evidence script
/// never handles a password, and the CI workflow emits the expected artifacts while excluding the
/// AD/AWS tests.</summary>
[Trait("Category", "Unit")]
public class EvidenceAndCiTests
{
    [Fact]
    public void Evidence_script_exists_produces_a_bundle_and_never_references_a_password()
    {
        string path = RepoPaths.Combine("tools", "run-integration-evidence.ps1");
        Assert.True(File.Exists(path), $"Expected evidence script at {path}");

        string script = File.ReadAllText(path);

        // The strongest guarantee that it can neither display nor copy AdAdminPassword: the word
        // "password" never appears in the script at all.
        Assert.DoesNotContain("password", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Start-Transcript", script);
        Assert.Contains("trx", script);
        Assert.Contains("manifest.json", script);
        Assert.Contains("artifacts/integration", script);
        Assert.Contains("AllowDirtyWorkingTree", script);
        Assert.Contains("evidenceStatus", script);
        Assert.Contains("artifactHashes", script);
        Assert.Contains("commitSha", script);
        Assert.Contains("finalInstanceState", script);
    }

    [Fact]
    public void Ci_workflow_produces_trx_vulnerability_json_and_uploaded_evidence()
    {
        string yml = File.ReadAllText(RepoPaths.Combine(".github", "workflows", "ci.yml"));

        Assert.Contains("trx", yml);
        Assert.Contains("--results-directory", yml);
        Assert.Contains("--vulnerable", yml);
        Assert.Contains("upload-artifact", yml);
        Assert.Contains("always()", yml);
        Assert.Contains("retention-days", yml);
        Assert.Contains("if-no-files-found", yml);
        // Least privilege on the workflow token.
        Assert.Contains("contents: read", yml);

        // Unit/HTTP tests run; the AD/AWS integration tests remain excluded from the standard CI.
        Assert.Contains("tests/CoreApi.UnitTests/CoreApi.UnitTests.csproj", yml);
        Assert.DoesNotContain("dotnet test tests/CoreApi.IntegrationTests", yml);
    }
}
