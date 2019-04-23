using System;
using System.Collections.Generic;
using CryptoBot.Arbitrage;
using CryptoBot.Exchanges;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.TcpDebug
{
    public class TcpResponse
    {
        [JsonProperty("type")]
        public string Type;

        public TcpResponse(string type)
        {
            Type = type;
        }

        [JsonIgnore]
        public static Dictionary<string, int> Priority = new Dictionary<string, int>
        {
            { "exchanges", 1 },
            { "snapshot", 2 },
            { "filter", 3 },
        };
    }

    public class ExchangesResponse : TcpResponse
    {
        [JsonProperty("exchanges")]
        public Exchange[] Exchanges;

        public ExchangesResponse(ExchangeNetwork network) : base("exchanges")
        {
            Exchanges = network.Exchanges;
        }
    }

    public class SnapshotResponse : TcpResponse
    {
        [JsonProperty("exchangeName")]
        public string ExchangeName;

        [JsonProperty("symbol")]
        public string Symbol;

        [JsonProperty("pair")]
        public CurrencyPair Pair;

        [JsonProperty("orderBook")]
        public OrderBook Orders;

        public SnapshotResponse(ExchangeNetwork network, SnapshotRequest request) : base("snapshot")
        {
            var exchange = network.GetExchange(request.ExchangeName);
            var orders = network.GetOrderBook(exchange, request.Pair);

            ExchangeName = exchange.Name;
            Pair = request.Pair;
            Orders = orders;
        }
    }
}