using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

/// <summary>One actual Godot process and its finite native-input mailbox; no world authority or gameplay transport.</summary>
internal sealed class NativeRosterProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stdout;
    private readonly Task<string> _stderr;
    private readonly CancellationToken _token;
    private int _sequence;
    private bool _closed;
    internal string DirectoryPath { get; }
    internal LocalWorldView View { get; private set; } = null!;
    internal JsonElement Last { get; private set; }
    internal List<JsonElement> Submissions { get; } = [];

    internal NativeRosterProcess(string directory, string invitation, string certificate, string project,
        bool rendered, CancellationToken token, bool expedition = false)
    {
        DirectoryPath = directory;
        Directory.CreateDirectory(directory);
        _token = token;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT")!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = project
        };
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET"))!;
        if (!rendered) start.ArgumentList.Add("--headless");
        foreach (string value in new[] { "--path", project, "res://scenes/NativePlayer.tscn", "--", expedition ? "--roster-expedition-probe" : "--roster-probe",
            invitation, certificate, directory }) start.ArgumentList.Add(value);
        _process = Process.Start(start) ?? throw new InvalidOperationException("Godot did not start.");
        _stdout = _process.StandardOutput.ReadToEndAsync();
        _stderr = _process.StandardError.ReadToEndAsync();
    }

    internal Task Opened() => Read(0, 30);

    internal async Task Send(string verb, string? direction = null, EntityId? target = null)
    {
        _token.ThrowIfCancellationRequested();
        int sequence = ++_sequence;
        string path = Path.Combine(DirectoryPath, $"{sequence:D4}.request.json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { Sequence = sequence, Verb = verb,
            Direction = direction, Target = target?.ToString() }));
        File.Move(temporary, path);
        await Read(sequence, verb == "observe" ? 5 : 20);
        if (verb == "close")
        {
            await _process.WaitForExitAsync(_token).WaitAsync(TimeSpan.FromSeconds(10), _token);
            _token.ThrowIfCancellationRequested();
            Assert.Equal(0, _process.ExitCode);
            Assert.DoesNotContain("ERROR:", await _stderr, StringComparison.Ordinal);
            _closed = true;
        }
    }

    private async Task Read(int sequence, int seconds)
    {
        string path = Path.Combine(DirectoryPath, $"{sequence:D4}.response.json");
        var watch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            _token.ThrowIfCancellationRequested();
            Assert.True(watch.Elapsed.TotalSeconds < seconds && !_process.HasExited,
                $"Native roster response {sequence} missing in {DirectoryPath}; exited={_process.HasExited}.");
            await Task.Delay(20, _token);
        }
        _token.ThrowIfCancellationRequested();
        Assert.True(watch.Elapsed.TotalSeconds < seconds, "Native roster response completed beyond its absolute bound.");
        using JsonDocument response = JsonDocument.Parse(File.ReadAllText(path));
        Last = response.RootElement.Clone();
        Assert.True(Last.GetProperty("Status").GetString() is "ready" or "passed",
            $"Native roster stage={Last.GetProperty("Stage").GetString()}, response={path}: {Last.GetProperty("Failure").GetString()}");
        Assert.Equal(sequence, Last.GetProperty("Sequence").GetInt32());
        Assert.False(Last.GetProperty("ObservedNativeFocus").GetBoolean());
        Assert.True(Last.GetProperty("InitialFocusGuard").GetBoolean());
        View = PlayerViewCodec.Decode(Convert.FromBase64String(Last.GetProperty("View").GetString()!));
        Submissions.Clear();
        Submissions.AddRange(Last.GetProperty("Submissions").EnumerateArray().Select(value => value.Clone()));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            File.WriteAllText(Path.Combine(DirectoryPath, "godot.stdout.log"), await _stdout);
            string stderr = await _stderr;
            File.WriteAllText(Path.Combine(DirectoryPath, "godot.stderr.log"), stderr);
            if (_closed) Assert.DoesNotContain("ERROR:", stderr, StringComparison.Ordinal);
        }
        finally { _process.Dispose(); }
    }

    internal static string BackgroundProject(string source, string output)
    {
        string target = Path.Combine(output, "background-client");
        Directory.CreateDirectory(target);
        foreach (string entry in Directory.EnumerateFileSystemEntries(source))
        {
            string name = Path.GetFileName(entry);
            if (name == "project.godot") continue;
            if (Directory.Exists(entry)) Directory.CreateSymbolicLink(Path.Combine(target, name), entry);
            else File.CreateSymbolicLink(Path.Combine(target, name), entry);
        }
        string config = File.ReadAllText(Path.Combine(source, "project.godot"));
        Assert.DoesNotContain("window/size/no_focus", config, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(target, "project.godot"),
            config.Replace("[display]", "[display]\nwindow/size/no_focus=true", StringComparison.Ordinal));
        return target;
    }
}
