using System;
using CryptoBot.Arbitrage;
using CryptoBot.Exchanges;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.TcpDebug.Json
{
    public class ExchangeConverter : JsonConverter<Exchange>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Exchange exchange, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("exchangeName");
            writer.WriteValue(exchange.Name);
            writer.WritePropertyName("markets");
            writer.WriteStartArray();

            foreach (var market in exchange.Markets)
                writer.WriteMarket(market.Value);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        
        public override Exchange ReadJson(JsonReader reader, Type objectType, Exchange existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new JsonReaderException("Cannot deserialize Exchange");
        }
    }

    public static class ExchangeJsonWriter
    {
        public static void WriteMarket(this JsonWriter writer, Market market)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("symbol");
            writer.WriteValue(market.Symbol);
            writer.WritePropertyName("pair");
            writer.WriteCurrencyPair(market.Pair);
            writer.WriteEndObject();
        }
    }
}