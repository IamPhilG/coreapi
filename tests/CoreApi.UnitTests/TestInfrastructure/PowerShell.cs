using System.ComponentModel;
using System.Diagnostics;

namespace CoreApi.UnitTests.TestInfrastructure;

/// <summary>Runs a PowerShell script (pwsh preferred, Windows PowerShell fallback) and returns its
/// exit code and combined output. Used to exercise the evidence script without any AWS/AD access.</summary>
public static class PowerShell
{
    private static readonly string Executable = ResolveExecutable();

    public static (int ExitCode, string Output) RunScript(string scriptPath, string workingDirectory, params string[] scriptArgs)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Fail fast instead of probing the (absent) instance metadata service, so `aws` calls in
        // the script return quickly during tests.
        psi.Environment["AWS_EC2_METADATA_DISABLED"] = "true";

        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath })
            psi.ArgumentList.Add(arg);
        foreach (string arg in scriptArgs)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {Executable}.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"PowerShell script timed out: {scriptPath}");
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string ResolveExecutable()
    {
        foreach (string candidate in new[] { "pwsh", "powershell" })
        {
            try
            {
                var psi = new ProcessStartInfo(candidate, "-NoProfile -Command exit 0")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var probe = Process.Start(psi);
                if (probe is null)
                    continue;
                probe.WaitForExit(15_000);
                if (probe.ExitCode == 0)
                    return candidate;
            }
            catch (Win32Exception)
            {
                // Not installed; try the next candidate.
            }
        }

        throw new InvalidOperationException("No PowerShell host (pwsh or powershell) is available.");
    }
}
