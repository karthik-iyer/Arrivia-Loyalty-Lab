using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LoyaltyLab.Api.Tests.Hosting;

public sealed class LoyaltyLabApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"loyaltylab-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoyaltyLab"] = $"Data Source={_dbPath}",
                ["DemoClock:Enabled"] = "true",
                ["DemoClock:UtcNow"] = "2026-03-15T12:00:00+00:00",
                ["Outbox:Dispatcher:Enabled"] = "false",
                ["SagaRecovery:Enabled"] = "false",
                ["Features:FaultInjection"] = "false",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // SQLite can hold the file until the process exits; temp cleanup is best-effort.
        }
    }
}
