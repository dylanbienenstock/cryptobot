using System;
using System.Collections.Generic;
using CryptoBot.TcpDebug.Json;
using CryptoBot.Exchanges;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.TcpDebug
{
    public struct TcpRequest
    {
        [JsonProperty("type", Required = Required.Always)]
        public string Type;

        [JsonProperty("body")]
        public string Body;

        [JsonIgnore]
        public static Dictionary<string, Type> BodyTypes = new Dictionary<string, Type>
        {
            { "exchanges", typeof(ExchangesRequest) },
            { "snapshot", typeof(SnapshotRequest) },
            { "filter", typeof(FilterRequest) },
        };
    }

    public struct ExchangesRequest {
        [JsonProperty("clientName", Required = Required.Always)]
        public string ClientName;
    }

    public struct SnapshotRequest
    {
        [JsonProperty("exchangeName", Required = Required.Always)]
        public string ExchangeName;

        [JsonProperty("pair", Required = Required.Always)]
        public CurrencyPair Pair;
    }

    public struct FilterRequest
    {
        [JsonProperty("exchangeName", Required = Required.Always)]
        public string ExchangeName;

        [JsonProperty("pair", Required = Required.Always)]
        public CurrencyPair Pair;

        [JsonProperty("enabled", Required = Required.Always)]
        public bool Enabled;
    }
}