extern alias PaymentSimHost;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LoyaltyLab.Resilience.Tests.PaymentSim;

public sealed class PaymentSimFactory : WebApplicationFactory<PaymentSimHost::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulator:LatencyMs"] = "0",
                ["Simulator:DeclineRate"] = "0",
                ["Simulator:TimeoutRate"] = "0",
                ["Simulator:TimeoutHangMs"] = "0",
            });
        });
    }
}
