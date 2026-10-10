using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TradingView.Client;

/// <summary>How to start the CLI: the executable, arguments placed before the command arguments, and a working directory.</summary>
/// <param name="FileName">Executable to start (<c>node.exe</c>, or <c>tv</c> outside Windows).</param>
/// <param name="PrefixArgs">Arguments inserted before the command arguments (the script path when running via node).</param>
/// <param name="WorkingDirectory">Directory of the tradingview-cli checkout when known.</param>
public sealed record TvCliLocation(string FileName, IReadOnlyList<string> PrefixArgs, string? WorkingDirectory);

/// <summary>
/// Finds the <c>tv</c> CLI. On Windows <c>tv</c> is an npm shim (<c>tv.cmd</c>) that cannot be started without a shell,
/// and <c>cmd.exe /c</c> mangles arguments (<c>!</c>, <c>^</c>, <c>&amp;</c>, quotes in JSON/Pine). So the locator resolves
/// <c>node.exe</c> plus the CLI's <c>index.js</c> and the transport starts those directly with an argument list.
/// </summary>
public sealed partial class TvCliLocator
{
    private const string ScriptRelativePath = "src/cli/index.js";
    private const string PackageName = "tradingview-cli";

    private readonly TvClientOptions _options;
    private readonly LocatorEnvironment _environment;
    private readonly Lock _gate = new();
    private TvCliLocation? _cached;

    public TvCliLocator(TvClientOptions options)
        : this(options, LocatorEnvironment.Current)
    {
    }

    internal TvCliLocator(TvClientOptions options, LocatorEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    /// <summary>Resolves (and caches) the CLI location.</summary>
    /// <exception cref="TvCliNotFoundException">Nothing usable was found; the message explains how to fix it.</exception>
    public TvCliLocation Locate()
    {
        lock (_gate)
        {
            return _cached ??= Resolve();
        }
    }

    private TvCliLocation Resolve()
    {
        // 1. Explicit configuration.
        if (!string.IsNullOrWhiteSpace(_options.CliScriptPath))
        {
            var script = Path.GetFullPath(_options.CliScriptPath);
            if (!_environment.FileExists(script))
            {
                throw new TvCliNotFoundException($"TvClientOptions.CliScriptPath points to a missing file: {script}");
            }

            var node = ResolveNode(null);
            return Create(node, script);
        }

        if (_environment.IsWindows)
        {
            // 2. Parse the npm shim of tv.cmd found on PATH.
            var shim = FindOnPath("tv.cmd");
            if (shim is not null && TryParseShim(shim, out var node, out var script))
            {
                return Create(node, script);
            }

            // 3. npm root -g
            var globalRoot = _environment.GetNpmGlobalRoot();
            if (!string.IsNullOrWhiteSpace(globalRoot))
            {
                var candidate = Path.Combine(globalRoot, PackageName, "src", "cli", "index.js");
                if (_environment.FileExists(candidate))
                {
                    return Create(ResolveNode(Path.GetDirectoryName(globalRoot)), candidate);
                }
            }
        }
        else
        {
            // 4. Outside Windows `tv` is directly executable.
            var tv = FindOnPath("tv");
            if (tv is not null)
            {
                return new TvCliLocation(tv, [], null);
            }
        }

        throw new TvCliNotFoundException(
            "The tradingview-cli 'tv' command was not found. Install it (git clone https://github.com/tradesdontlie/tradingview-cli, " +
            "npm install, npm link) and make sure it is on PATH, or set TvClientOptions.NodePath and TvClientOptions.CliScriptPath.");
    }

    private string ResolveNode(string? hintDirectory)
    {
        if (!string.IsNullOrWhiteSpace(_options.NodePath))
        {
            return _options.NodePath;
        }

        if (hintDirectory is not null)
        {
            var sibling = Path.Combine(hintDirectory, _environment.IsWindows ? "node.exe" : "node");
            if (_environment.FileExists(sibling))
            {
                return sibling;
            }
        }

        return FindOnPath(_environment.IsWindows ? "node.exe" : "node")
            ?? throw new TvCliNotFoundException("node was not found on PATH. Install Node.js 20+ or set TvClientOptions.NodePath.");
    }

    private TvCliLocation Create(string node, string script)
    {
        var workingDirectory = _options.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            // <root>/src/cli/index.js -> <root>
            workingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(script)));
        }

        return new TvCliLocation(node, [script], workingDirectory);
    }

    private bool TryParseShim(string shimPath, out string node, out string script)
    {
        node = script = string.Empty;
        var shimDir = Path.GetDirectoryName(shimPath)!;
        string text;
        try
        {
            text = _environment.ReadAllText(shimPath);
        }
        catch (IOException)
        {
            return false;
        }

        // ... "%dp0%\node_modules\tradingview-cli\src\cli\index.js" %*
        var match = ShimScriptRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var relative = match.Groups["rel"].Value.Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(shimDir, relative));
        if (!_environment.FileExists(candidate))
        {
            return false;
        }

        script = candidate;
        node = ResolveNode(shimDir);
        return true;
    }

    private string? FindOnPath(string fileName)
    {
        foreach (var dir in _environment.PathDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var candidate = Path.Combine(dir.Trim('"'), fileName);
            if (_environment.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    [GeneratedRegex("""%dp0%[\\/]*(?<rel>[^"%]*index\.js)""", RegexOptions.IgnoreCase)]
    private static partial Regex ShimScriptRegex();

    /// <summary>Seam over the machine (OS, PATH, file system, npm) so the locator is testable.</summary>
    internal sealed record LocatorEnvironment(
        bool IsWindows,
        IReadOnlyList<string> PathDirectories,
        Func<string, bool> FileExists,
        Func<string, string> ReadAllText,
        Func<string?> GetNpmGlobalRoot)
    {
        public static LocatorEnvironment Current { get; } = new(
            OperatingSystem.IsWindows(),
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator),
            File.Exists,
            File.ReadAllText,
            RunNpmRootGlobal);

        private static string? RunNpmRootGlobal()
        {
            try
            {
                var psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "npm")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                if (OperatingSystem.IsWindows())
                {
                    psi.ArgumentList.Add("/c");
                    psi.ArgumentList.Add("npm root -g");
                }
                else
                {
                    psi.ArgumentList.Add("root");
                    psi.ArgumentList.Add("-g");
                }

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                if (!process.WaitForExit(10_000))
                {
                    process.Kill(entireProcessTree: true);
                    return null;
                }

                return process.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
