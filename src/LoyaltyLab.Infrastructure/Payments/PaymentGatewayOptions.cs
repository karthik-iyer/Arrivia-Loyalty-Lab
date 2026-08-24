namespace LoyaltyLab.Infrastructure.Payments;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentSim";

    public string BaseUrl { get; set; } = "http://localhost:5190/";
}
