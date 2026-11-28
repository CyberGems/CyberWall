using System.Diagnostics;

namespace CyberWall.UI.Services;

public enum GitHubStarResult { Starred, Opened, Failed }

public static class GitHubStarService
{
    private const string Repository = "CyberGems/CyberWall";
    private static readonly object Gate = new();
    private static Task<GitHubStarResult>? _pending;

    public static Task<GitHubStarResult> StarAsync()
    {
        lock (Gate)
        {
            return _pending ??= Task.Run(RunAsync);
        }
    }

    private static async Task<GitHubStarResult> RunAsync()
    {
        try
        {
            return await RunOperationAsync(TryCliAsync, () =>
                Process.Start(new ProcessStartInfo($"https://github.com/{Repository}") { UseShellExecute = true }))
                .ConfigureAwait(false);
        }
        finally
        {
            lock (Gate) { _pending = null; }
        }
    }

    internal static async Task<GitHubStarResult> RunOperationAsync(Func<Task<bool>> tryCli, Action openRepository)
    {
        try
        {
            if (await tryCli().ConfigureAwait(false)) return GitHubStarResult.Starred;
            openRepository();
            return GitHubStarResult.Opened;
        }
        catch (Exception error)
        {
            Trace.TraceError("GitHub star failed: {0}", error);
            return GitHubStarResult.Failed;
        }
    }

    private static async Task<bool> TryCliAsync()
    {
        var start = new ProcessStartInfo("gh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in new[] { "api", "--hostname", "github.com", "--method", "PUT", $"user/starred/{Repository}" })
            start.ArgumentList.Add(argument);
        start.Environment["GH_PROMPT_DISABLED"] = "1";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) return false;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
        process.StandardInput.Close();
        var output = process.StandardOutput.BaseStream.CopyToAsync(System.IO.Stream.Null);
        var errors = process.StandardError.BaseStream.CopyToAsync(System.IO.Stream.Null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(output, errors).ConfigureAwait(false);
            return false;
        }
        await Task.WhenAll(output, errors).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}
