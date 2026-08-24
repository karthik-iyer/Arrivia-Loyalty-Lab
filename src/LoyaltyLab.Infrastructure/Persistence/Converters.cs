using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoyaltyLab.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoyaltyLab.Infrastructure.Persistence;

internal static class PersistenceJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new PercentJsonConverter(),
            new CurrencyJsonConverter(),
            new MoneyJsonConverter(),
            new PricingRuleIdJsonConverter(),
        },
    };

    public static ValueConverter<T, string> JsonConverter<T>() =>
        new(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<T>(v, Options)!);
}

internal sealed class PercentJsonConverter : JsonConverter<Percent>
{
    public override Percent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Percent.From(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, Percent value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Currency.Of(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Code);
}

internal sealed class MoneyConverter : ValueConverter<Money, string>
{
    public MoneyConverter()
        : base(v => Format(v), v => Parse(v))
    {
    }

    private static string Format(Money value) =>
        value.Amount.ToString(CultureInfo.InvariantCulture) + "|" + value.Currency.Code;

    private static Money Parse(string stored)
    {
        var parts = stored.Split('|');
        return Money.Of(decimal.Parse(parts[0], CultureInfo.InvariantCulture), Currency.Of(parts[1]));
    }
}

internal sealed class DestinationConverter : ValueConverter<LoyaltyLab.Domain.Catalog.Destination, string>
{
    public DestinationConverter()
        : base(v => Format(v), v => Parse(v))
    {
    }

    private static string Format(LoyaltyLab.Domain.Catalog.Destination value) =>
        value.Code + "|" + value.DisplayName;

    private static LoyaltyLab.Domain.Catalog.Destination Parse(string stored)
    {
        var separator = stored.IndexOf('|', StringComparison.Ordinal);
        return new LoyaltyLab.Domain.Catalog.Destination(stored[..separator], stored[(separator + 1)..]);
    }
}

internal sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Money must be a JSON object.");
        }

        decimal? amount = null;
        Currency? currency = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "amount", StringComparison.OrdinalIgnoreCase))
            {
                amount = reader.GetDecimal();
            }
            else if (string.Equals(name, "currency", StringComparison.OrdinalIgnoreCase))
            {
                currency = Currency.Of(reader.GetString()!);
            }
        }

        if (amount is null || currency is null)
        {
            throw new JsonException("Money requires amount and currency.");
        }

        return Money.Of(amount.Value, currency.Value);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amount", value.Amount);
        writer.WriteString("currency", value.Currency.Code);
        writer.WriteEndObject();
    }
}

internal sealed class PricingRuleIdJsonConverter : JsonConverter<LoyaltyLab.Domain.Common.PricingRuleId>
{
    public override LoyaltyLab.Domain.Common.PricingRuleId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        LoyaltyLab.Domain.Common.PricingRuleId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
