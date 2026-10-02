using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenFantasy.Server;

/// <summary>Creates only the explicit listener: no application settings, environment endpoints or logging providers.</summary>
internal static class PlayerWebSocketApplication
{
    internal static WebApplication Create(PlayerWebSocketServerOptions options)
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = Environments.Production,
            ApplicationName = typeof(PlayerWebSocketServer).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        // The embedding application owns OS signals. Multiple in-process listeners must not register console lifetimes.
        builder.Services.AddSingleton<IHostLifetime, PlayerListenerLifetime>();
        // The public StopAsync token bounds its caller's wait. Cleanup itself must still join already-admitted reads.
        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = Timeout.InfiniteTimeSpan);
        builder.WebHost.UseKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxConcurrentConnections = 16;
            kestrel.Limits.MaxConcurrentUpgradedConnections = PlayerWebSocketServer.MaximumPlayerRequests;
            kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
            kestrel.Limits.MaxRequestHeadersTotalSize = 8 * 1024;
            kestrel.Limits.MaxRequestHeaderCount = 32;
            kestrel.Limits.MaxRequestLineSize = 1024;
            kestrel.Limits.MaxRequestBodySize = 0;
            kestrel.Listen(options.ListenAddress, options.Port, endpoint =>
            {
                endpoint.Protocols = HttpProtocols.Http1;
                endpoint.UseHttps(https =>
                {
                    https.ServerCertificate = options.Certificate;
                    https.HandshakeTimeout = TimeSpan.FromSeconds(5);
                });
            });
        });
        return builder.Build();
    }
}
