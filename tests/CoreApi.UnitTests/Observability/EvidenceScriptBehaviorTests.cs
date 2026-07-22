using System.Diagnostics;
using System.Text.Json;
using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Observability;

/// <summary>
/// Behavioural tests for tools/run-integration-evidence.ps1 that start NO AWS/AD resources: the
/// self-test hooks (-SkipTestExecution / -SimulateTestExitCode) exercise the provenance gate,
/// evidence-status logic, exit-code rule, and manifest/hash structure without running dotnet or AWS.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EvidenceScriptBehaviorTests
{
    private static readonly string ScriptPath = RepoPaths.Combine("tools", "run-integration-evidence.ps1");

    [Fact]
    public void Refuses_a_dirty_working_tree_without_the_override()
    {
        // The dirtiness is created here, in a throwaway repo -- never inherited from the checkout
        // running the tests, which is clean in CI and arbitrary locally.
        using var repo = new TempGitRepo(ScriptPath);
        repo.MakeDirty();

        (int exit, string output) = repo.RunScript("-SkipTestExecution");

        Assert.Equal(3, exit);
        Assert.Contains("Refusing", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dirty_tree_with_override_produces_a_development_only_bundle()
    {
        using var repo = new TempGitRepo(ScriptPath);
        repo.MakeDirty();

        (int exit, string output) = repo.RunScript("-AllowDirtyWorkingTree", "-SkipTestExecution");

        Assert.Equal(0, exit);
        JsonElement manifest = ReadManifest(output);
        Assert.Equal("development-only", manifest.GetProperty("evidenceStatus").GetString());
        Assert.False(manifest.GetProperty("workingTreeClean").GetBoolean());
        Assert.False(string.IsNullOrEmpty(manifest.GetProperty("gitStatusPorcelain").GetString()));
        AssertManifestShapeAndSafety(manifest);
    }

    [Fact]
    public void Clean_tree_passing_tests_but_no_aws_is_incomplete_and_returns_2()
    {
        using var repo = new TempGitRepo(ScriptPath);
        (int exit, string output) = repo.RunScript("-SkipTestExecution", "-SimulateTestExitCode", "0");

        Assert.Equal(2, exit);
        JsonElement manifest = ReadManifest(output);
        Assert.Equal("incomplete", manifest.GetProperty("evidenceStatus").GetString());
        Assert.Equal("passed", manifest.GetProperty("testResult").GetString());
        Assert.Equal(0, manifest.GetProperty("testExitCode").GetInt32());
        AssertManifestShapeAndSafety(manifest);
    }

    [Fact]
    public void Incomplete_evidence_returns_0_when_explicitly_allowed_for_local_trials()
    {
        using var repo = new TempGitRepo(ScriptPath);
        (int exit, string output) = repo.RunScript("-SkipTestExecution", "-AllowIncompleteEvidence");

        Assert.Equal(0, exit);
        // Never upgraded to 'complete' by the local override.
        Assert.Equal("incomplete", ReadManifest(output).GetProperty("evidenceStatus").GetString());
    }

    [Fact]
    public void Failed_tests_take_priority_over_evidence_completeness_in_the_exit_code()
    {
        using var repo = new TempGitRepo(ScriptPath);
        (int exit, string output) = repo.RunScript("-SkipTestExecution", "-SimulateTestExitCode", "7");

        Assert.Equal(7, exit);
        JsonElement manifest = ReadManifest(output);
        Assert.Equal("failed", manifest.GetProperty("testResult").GetString());
        Assert.Equal(7, manifest.GetProperty("testExitCode").GetInt32());
    }

    private static void AssertManifestShapeAndSafety(JsonElement manifest)
    {
        foreach (string field in new[]
                 {
                     "evidenceVersion", "evidenceStatus", "commitSha", "branch", "workingTreeClean",
                     "gitStatusPorcelain", "timestampStartedUtc", "timestampCompletedUtc", "dotnetVersion",
                     "awsAccount", "awsPrincipalArn", "awsRegion", "instanceId", "ldapProtocol", "ldapPort",
                     "testResult", "testExitCode", "finalInstanceState", "artifactHashes",
                 })
            Assert.True(manifest.TryGetProperty(field, out _), $"manifest missing field '{field}'");

        JsonElement hashes = manifest.GetProperty("artifactHashes");
        foreach (string artifact in new[] { "integration-tests.trx", "transcript.log", "dotnet-test-output.log" })
        {
            string? hash = hashes.GetProperty(artifact).GetString();
            Assert.StartsWith("sha256:", hash);
        }

        string raw = manifest.GetRawText();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LDAP-PASSWORD-SENTINEL", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ReadManifest(string scriptOutput)
    {
        string evidenceDir = ParseEvidenceDir(scriptOutput);
        string manifestPath = Path.Combine(evidenceDir, "manifest.json");
        Assert.True(File.Exists(manifestPath), $"manifest not found at {manifestPath}");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return doc.RootElement.Clone();
    }

    private static string ParseEvidenceDir(string output)
    {
        foreach (string line in output.Split('\n'))
            if (line.Contains("Evidence directory:", StringComparison.Ordinal))
                return line.Split("Evidence directory:", 2, StringSplitOptions.None)[1].Trim();
        throw new InvalidOperationException($"Could not find the evidence directory in script output:\n{output}");
    }

    private sealed class TempGitRepo : IDisposable
    {
        public string Root { get; }
        private readonly string _scriptPath;

        public TempGitRepo(string sourceScript)
        {
            Root = Path.Combine(Path.GetTempPath(), $"coreapi-evidence-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(Root, "tools"));
            _scriptPath = Path.Combine(Root, "tools", "run-integration-evidence.ps1");
            File.Copy(sourceScript, _scriptPath);
            Run("git", "init");
            // A fresh repo with only the copied script committed => clean working tree.
            Run("git", "add", ".");
            Run("git", "-c", "user.email=ci@example.test", "-c", "user.name=CI", "commit", "-m", "init");
        }

        public (int ExitCode, string Output) RunScript(params string[] scriptArgs) =>
            PowerShell.RunScript(_scriptPath, Root, scriptArgs);

        /// <summary>
        /// Makes this throwaway repository's working tree dirty on purpose, by adding an untracked
        /// file: `git status --porcelain` then reports it, which is exactly what the script reads.
        /// Tests that need a dirty tree call this instead of relying on the checkout they run in.
        /// </summary>
        public void MakeDirty()
        {
            File.WriteAllText(Path.Combine(Root, "uncommitted-marker.txt"), "deliberately untracked\n");
        }

        private void Run(string exe, params string[] args)
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.WaitForExit(30000);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
        }
    }
}
