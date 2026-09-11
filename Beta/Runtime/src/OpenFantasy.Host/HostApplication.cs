using System.Runtime.InteropServices;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;
using Microsoft.Data.Sqlite;

namespace OpenFantasy.Host;

/// <summary>Routes host commands and writes their results to the configured output.</summary>
internal sealed class HostApplication
{
    private readonly TextWriter _output;
    private readonly ContractInspector _contracts = new();
    private readonly LocalWorldOperator _worlds = new();
    private readonly RecordedWorldOperator _history = new();
    private readonly WatershedOperator _watersheds = new();
    private readonly ComposedWorldOperator _composed = new();

    public HostApplication(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            string? report = arguments switch
            {
                ["--inspect-foundation"] => CreateFoundationReport(),
                ["--inspect-command", var path] => _contracts.InspectCommand(path),
                ["--inspect-custody", var path] => _contracts.InspectCustody(path),
                ["--inspect-package", var manifest, var payload] => _contracts.InspectPackage(manifest, payload),
                ["--create-world", var database, var genesis] => _worlds.Create(database, genesis),
                ["--inspect-world", var database] => _worlds.Inspect(database),
                ["--inspect-recorded-world", var database, var revision] => _history.Inspect(database, revision),
                ["--inspect-watershed", var path] => _watersheds.Inspect(path),
                ["--create-watershed-world", var database, var basis, var watershed] =>
                    _composed.CreateWatershedWorld(database, basis, watershed),
                ["--execute-world-step", var database, var step] => _composed.ExecuteStep(database, step),
                ["--probe-world-step-crash", var database, var step, var boundary] =>
                    _composed.ExecuteStep(database, step, boundary),
                ["--execute-local-command", var database, var command, var principal, var session] =>
                    _worlds.Execute(database, command, principal, session),
                ["--probe-commit-crash", var database, var command, var principal, var session, var boundary] =>
                    _worlds.Execute(database, command, principal, session, boundary),
                _ => null
            };
            if (report is not null)
            {
                _output.WriteLine(report);
                return 0;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or InvalidDataException or FormatException or JsonException or ArgumentException
            or AuthorityDispatchException or WorldRecoveryLimitException or SqliteException)
        {
            _output.WriteLine("Inspection failed: " + error.Message);
            return 1;
        }

        WriteUsage();
        return arguments.Length == 0 ? 0 : 2;
    }

    private void WriteUsage()
    {
        _output.WriteLine("OpenFantasy host\nUsage: OpenFantasy.Host --inspect-foundation");
        _output.WriteLine("       OpenFantasy.Host --inspect-command <command.bin>");
        _output.WriteLine("       OpenFantasy.Host --inspect-custody <snapshot.bin>");
        _output.WriteLine("       OpenFantasy.Host --inspect-package <manifest.json> <payload-file>");
        _output.WriteLine("       OpenFantasy.Host --create-world <world.sqlite> <genesis.bin>");
        _output.WriteLine("       OpenFantasy.Host --inspect-world <world.sqlite>");
        _output.WriteLine("       OpenFantasy.Host --inspect-recorded-world <composed-world.sqlite> <commit-revision>");
        _output.WriteLine("       OpenFantasy.Host --inspect-watershed <prepared-basin.bin>");
        _output.WriteLine("       OpenFantasy.Host --execute-local-command <world.sqlite> <command.bin> <principal-id> <session-id>");
        _output.WriteLine("       OpenFantasy.Host --create-watershed-world <world.sqlite> <construction-genesis.bin> <prepared-basin.bin>");
        _output.WriteLine("       OpenFantasy.Host --execute-world-step <world.sqlite> <host-selected-step.bin>");
        _output.WriteLine("These commands require exclusive local database access; they do not authenticate network clients.");
    }

    private static string CreateFoundationReport()
    {
        var seed = new byte[32];
        seed[^1] = 42;
        var identity = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
        var random = new CounterRandomV1(seed, "rules-v1", "foundation", identity);
        var midnight = new WorldInstant(-1, WorldInstant.MillisecondsPerDay - 1).AddMilliseconds(1);

        return JsonSerializer.Serialize(new
        {
            Contract = "foundation-v1",
            Runtime = RuntimeInformation.FrameworkDescription,
            Entity = identity.ToString(),
            Sample = random.NextUInt64(),
            Day = midnight.Days,
            Millisecond = midnight.MillisecondsWithinDay
        });
    }
}
