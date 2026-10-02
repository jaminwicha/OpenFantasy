using OpenFantasy.Host;
using System.Runtime.InteropServices;

if (args is ["--serve-cargo-world", var settings])
{
    using var stop = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, signal) => { signal.Cancel = true; stop.Cancel(); };
    Console.CancelKeyPress += cancel;
    using var terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM,
        signal => { signal.Cancel = true; stop.Cancel(); });
    try { return await new ServeCargoWorldCommand(Console.Out, Console.Error).RunAsync(settings, stop.Token); }
    finally { Console.CancelKeyPress -= cancel; }
}

var application = new HostApplication(Console.Out);
return application.Run(args);
