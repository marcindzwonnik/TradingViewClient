namespace TradingView.Client.Tests;

public class TvCliLocatorTests
{
    // Verbatim shape of the shim `npm link` generates on Windows.
    private const string NpmShim = """
        @ECHO off
        GOTO start
        :find_dp0
        SET dp0=%~dp0
        EXIT /b
        :start
        SETLOCAL
        CALL :find_dp0

        IF EXIST "%dp0%\node.exe" (
          SET "_prog=%dp0%\node.exe"
        ) ELSE (
          SET "_prog=node"
          SET PATHEXT=%PATHEXT:;.JS;=;%
        )

        endLocal & goto #_undefined_# 2>NUL || title %COMSPEC% & "%_prog%"  "%dp0%\node_modules\tradingview-cli\src\cli\index.js" %*
        """;

    private static TvCliLocator.LocatorEnvironment Windows(
        HashSet<string> files,
        string[] path,
        string? shimText = null,
        string? npmRoot = null)
        => new(
            IsWindows: true,
            PathDirectories: path,
            FileExists: files.Contains,
            ReadAllText: file => files.Contains(file) && shimText is not null ? shimText : throw new FileNotFoundException(file),
            GetNpmGlobalRoot: () => npmRoot);

    private static string P(params string[] parts) => Path.GetFullPath(Path.Combine(parts));

    [Fact]
    public void Resolves_node_and_script_from_the_npm_shim()
    {
        var dir = P(Path.GetTempPath(), "nodejs");
        var script = P(dir, "node_modules", "tradingview-cli", "src", "cli", "index.js");
        var files = new HashSet<string> { Path.Combine(dir, "tv.cmd"), Path.Combine(dir, "node.exe"), script };
        var locator = new TvCliLocator(new TvClientOptions(), Windows(files, [dir], NpmShim));

        var location = locator.Locate();

        Assert.Equal(Path.Combine(dir, "node.exe"), location.FileName);
        Assert.Equal([script], location.PrefixArgs);
        Assert.Equal(P(dir, "node_modules", "tradingview-cli"), location.WorkingDirectory);
    }

    [Fact]
    public void Falls_back_to_node_on_path_when_shim_dir_has_no_node()
    {
        var shimDir = P(Path.GetTempPath(), "shim");
        var nodeDir = P(Path.GetTempPath(), "elsewhere");
        var script = P(shimDir, "node_modules", "tradingview-cli", "src", "cli", "index.js");
        var files = new HashSet<string> { Path.Combine(shimDir, "tv.cmd"), Path.Combine(nodeDir, "node.exe"), script };
        var locator = new TvCliLocator(new TvClientOptions(), Windows(files, [shimDir, nodeDir], NpmShim));

        Assert.Equal(Path.Combine(nodeDir, "node.exe"), locator.Locate().FileName);
    }

    [Fact]
    public void Falls_back_to_npm_global_root_when_shim_is_unusable()
    {
        var dir = P(Path.GetTempPath(), "nodejs2");
        var globalRoot = P(dir, "node_modules");
        var script = P(globalRoot, "tradingview-cli", "src", "cli", "index.js");
        var files = new HashSet<string> { Path.Combine(dir, "node.exe"), script };
        var locator = new TvCliLocator(new TvClientOptions(), Windows(files, [], npmRoot: globalRoot));

        var location = locator.Locate();

        Assert.Equal(Path.Combine(dir, "node.exe"), location.FileName);
        Assert.Equal([script], location.PrefixArgs);
    }

    [Fact]
    public void Explicit_options_win_over_discovery()
    {
        var node = P(Path.GetTempPath(), "mynode", "node.exe");
        var script = P(Path.GetTempPath(), "cli", "src", "cli", "index.js");
        var files = new HashSet<string> { script, node };
        var locator = new TvCliLocator(
            new TvClientOptions { NodePath = node, CliScriptPath = script, WorkingDirectory = "C:\\work" },
            Windows(files, []));

        var location = locator.Locate();

        Assert.Equal(node, location.FileName);
        Assert.Equal([script], location.PrefixArgs);
        Assert.Equal("C:\\work", location.WorkingDirectory);
    }

    [Fact]
    public void Missing_explicit_script_is_reported()
    {
        var locator = new TvCliLocator(
            new TvClientOptions { CliScriptPath = P(Path.GetTempPath(), "nope", "index.js") },
            Windows([], []));

        var ex = Assert.Throws<TvCliNotFoundException>(locator.Locate);

        Assert.Contains("CliScriptPath", ex.Message);
    }

    [Fact]
    public void Nothing_found_gives_actionable_message()
    {
        var locator = new TvCliLocator(new TvClientOptions(), Windows([], []));

        var ex = Assert.Throws<TvCliNotFoundException>(locator.Locate);

        Assert.Contains("npm link", ex.Message);
        Assert.Contains("CliScriptPath", ex.Message);
    }

    [Fact]
    public void Non_windows_uses_tv_from_path()
    {
        var bin = P(Path.GetTempPath(), "bin");
        var tv = Path.Combine(bin, "tv");
        var env = new TvCliLocator.LocatorEnvironment(false, [bin], f => f == tv, _ => "", () => null);

        var location = new TvCliLocator(new TvClientOptions(), env).Locate();

        Assert.Equal(tv, location.FileName);
        Assert.Empty(location.PrefixArgs);
    }

    [Fact]
    public void Result_is_cached()
    {
        var calls = 0;
        var dir = P(Path.GetTempPath(), "nodejs3");
        var script = P(dir, "node_modules", "tradingview-cli", "src", "cli", "index.js");
        var files = new HashSet<string> { Path.Combine(dir, "tv.cmd"), Path.Combine(dir, "node.exe"), script };
        var env = Windows(files, [dir], NpmShim) with
        {
            FileExists = f =>
            {
                calls++;
                return files.Contains(f);
            },
        };
        var locator = new TvCliLocator(new TvClientOptions(), env);

        var first = locator.Locate();
        var callsAfterFirst = calls;
        var second = locator.Locate();

        Assert.Same(first, second);
        Assert.Equal(callsAfterFirst, calls);
    }
}
