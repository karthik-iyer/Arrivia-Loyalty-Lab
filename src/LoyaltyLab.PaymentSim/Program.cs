using System.Text.Json.Serialization;
using LoyaltyLab.PaymentSim;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<PaymentProcessor>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPaymentEndpoints();

await app.RunAsync();

public partial class Program;
