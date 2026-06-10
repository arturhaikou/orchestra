using System.Diagnostics;
using System.Text.Json;

namespace Orchestra.Infrastructure.Agents;

internal static class SkillScriptRunner
{
    internal static async Task<object?> RunScriptAsync(
        string scriptPath,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        var args = arguments?.Deserialize<string[]>() ?? [];
        var ext = Path.GetExtension(scriptPath).ToLowerInvariant();

        var (fileName, scriptArg) = ext switch
        {
            ".ps1" => ("powershell", $"-NoProfile -File \"{scriptPath}\""),
            ".py" => ("python", $"\"{scriptPath}\""),
            ".sh" => ("bash", $"\"{scriptPath}\""),
            ".bat" or ".cmd" => ("cmd", $"/c \"{scriptPath}\""),
            _ => throw new NotSupportedException($"Script extension '{ext}' is not supported by the skill runner.")
        };

        var allArgs = args.Length > 0
            ? $"{scriptArg} {string.Join(" ", args.Select(a => $"\"{a}\""))}"
            : scriptArg;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = allArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Skill script '{Path.GetFileName(scriptPath)}' exited with code {process.ExitCode}. Stderr: {stderr}");
        }

        return (object?)stdout;
    }
}
