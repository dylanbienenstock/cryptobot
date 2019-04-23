using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.TcpDebug.Json
{
    public class CurrencyPairConverter : JsonConverter<CurrencyPair>
    {
        public override bool CanRead => true;

        public override void WriteJson(JsonWriter writer, CurrencyPair pair, JsonSerializer serializer) => 
            writer.WriteCurrencyPair(pair);

        public override CurrencyPair ReadJson(JsonReader reader, Type objectType, CurrencyPair existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            
            return new CurrencyPair(
                token["base"].ToString(), 
                token["quote"].ToString()
            );
        }
    }

    public static class CurrencyPairJsonWriter
    {
        public static void WriteCurrencyPair(this JsonWriter writer, CurrencyPair pair)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("base");
            writer.WriteValue(Enum.GetName(typeof(Currency), pair.Base));
            writer.WritePropertyName("quote");
            writer.WriteValue(Enum.GetName(typeof(Currency), pair.Quote));
            writer.WriteEndObject();
        }
    }
}