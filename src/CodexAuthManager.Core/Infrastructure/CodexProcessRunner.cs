using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Infrastructure;

/// <summary>
/// Runs the Codex process and retrieves usage statistics from the session history file
/// </summary>
public class CodexProcessRunner : ICodexProcessRunner
{
    public async Task<string> RunCodexWithStatusAsync()
    {
        var codexCommand = GetCodexCommand();

        // Run codex exec to trigger a session
        var startInfo = new ProcessStartInfo
        {
            FileName = codexCommand,
            Arguments = "exec --yolo --skip-git-repo-check hi",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion with timeout
        var exitTask = process.WaitForExitAsync();
        var completedTask = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(30)));

        if (completedTask != exitTask)
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException("Codex process timed out");
        }

        // Wait a moment for the session file to be fully written
        await Task.Delay(500);

        // Find and read the latest session file
        var sessionFile = FindLatestSessionFile();
        if (sessionFile == null)
        {
            throw new InvalidOperationException(
                "Could not find Codex session file. " +
                "Please ensure Codex is properly installed and configured.");
        }

        var sessionContent = await File.ReadAllTextAsync(sessionFile);
        return sessionContent;
    }

    private string? FindLatestSessionFile()
    {
        // Session files are stored in: %USERPROFILE%\.codex\sessions\YYYY\MM\DD\
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sessionsDir = Path.Combine(userProfile, ".codex", "sessions");

        if (!Directory.Exists(sessionsDir))
        {
            return null;
        }

        // Get today's date directory
        var now = DateTime.Now;
        var todayDir = Path.Combine(sessionsDir, now.Year.ToString(), now.Month.ToString("00"), now.Day.ToString("00"));

        if (!Directory.Exists(todayDir))
        {
            return null;
        }

        // Find the most recent .jsonl file
        var jsonlFiles = Directory.GetFiles(todayDir, "*.jsonl")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToArray();

        return jsonlFiles.FirstOrDefault();
    }

    private string GetCodexCommand()
    {
        // On Windows, we need to use codex.cmd
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "codex.cmd";
        }

        return "codex";
    }
}
