using System;
using System.Collections.Generic;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.TcpDebug
{
    public class UpdateFilter
    {
        private Dictionary<Exchange, Dictionary<CurrencyPair, bool>> Subscriptions;
        private ExchangeNetwork Network;

        public UpdateFilter(ExchangeNetwork network)
        {
            Subscriptions = new Dictionary<Exchange, Dictionary<CurrencyPair, bool>>();
            Network = network;
        }

        public bool Get(Exchange exchange, CurrencyPair pair)
        {
            if (!Subscriptions.ContainsKey(exchange)) return false;
            if (!Subscriptions[exchange].ContainsKey(pair)) return false;

            return Subscriptions[exchange][pair];
        }

        public bool Allows(CurrencyOrder order) => Get(order.Exchange, order.Pair);

        public void Set(Exchange exchange, CurrencyPair pair, bool enabled)
        {
            if (!Subscriptions.ContainsKey(exchange))
                Subscriptions.Add(exchange, new Dictionary<CurrencyPair, bool>());

            Subscriptions[exchange][pair] = enabled;
        }

        public SnapshotResponse SetFrom(FilterRequest req)
        {
            var exchange = Network.GetExchange(req.ExchangeName);
            Set(exchange, req.Pair, req.Enabled);
        
            if (req.Enabled)
            {
                return new SnapshotResponse(Network, new SnapshotRequest()
                {
                    ExchangeName = req.ExchangeName,
                    Pair = req.Pair
                });
            }

            return null;
        }

        public void Enable(Exchange exchange, CurrencyPair pair) => 
            Set(exchange, pair, true);

        public void Disable(Exchange exchange, CurrencyPair pair) => 
            Set(exchange, pair, false);
    }
}