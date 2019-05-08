using System;
using System.Collections.Generic;
using System.Threading;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators;

namespace CryptoBot.Arbitrage
{
    public struct CointegrableEdge
    {
        public Market Home;
        public Market Away;
        public TimeSeries<decimal> ReverseHistory;
        public SimpleMovingAverage ReverseSMA;

        public CointegrableEdge(Market home, Market away)
        {
            Home = home;
            Away = away;
            ReverseHistory = new TimeSeries<decimal>(Cointegrator.HistoryTimespan);
            ReverseSMA = new SimpleMovingAverage(ReverseHistory);
        }
    }

    public class Cointegrator
    {
        public static TimeSpan HistoryTimespan = new TimeSpan(0, 0, 10);
        private ExchangeNetwork _network;
        private List<CointegrableEdge> _edges;
        private Thread _thread;

        public Cointegrator(ExchangeNetwork network)
        {
            _network = network;
            _edges = new List<CointegrableEdge>();
            _thread = new Thread(Start);
            _thread.Start();
        }

        public void Start()
        {
            BuildGraph();

            while (true)
            {
                TestMarkets();
                Thread.Sleep(1);
            }
        }

        private void BuildGraph()
        {
            foreach (var homeExchange in _network.Exchanges)
            {
                foreach (var awayExchange in _network.Exchanges)
                {
                    if (homeExchange == awayExchange) continue;

                    foreach (var homeMarket in homeExchange.Markets.Values)
                    {
                        foreach (var awayMarket in awayExchange.Markets.Values)
                        {
                            if (homeMarket == awayMarket) continue;
                            if (homeMarket.Pair.ToGenericSymbol() != awayMarket.Pair.ToGenericSymbol()) continue;

                            _edges.Add(new CointegrableEdge(homeMarket, awayMarket));
                        }   
                    }
                }
            }
        }

        public decimal GetTransactionResult(Market from, Market to, bool withFees = true)
        {
            if (withFees) return
              from.BestBid * (1 - from.Exchange.Fee)
              - to.BestAsk * (1 + to.Exchange.Fee);

            return from.BestBid - to.BestAsk;
        }

        public void TestMarkets()
        {
            foreach (var edge in _edges)
            {
                if (edge.Home.Orders.Bids.Tail == null) continue;
                if (edge.Home.Orders.Asks.Tail == null) continue;
                if (edge.Away.Orders.Bids.Tail == null) continue;
                if (edge.Away.Orders.Asks.Tail == null) continue;

                decimal result = GetTransactionResult(edge.Home, edge.Away);
                decimal reverseResult = GetTransactionResult(edge.Away, edge.Home);

                edge.ReverseHistory.Record(reverseResult);

                if (!edge.ReverseHistory.Complete) continue;

                decimal totalResult = result + edge.ReverseSMA.Value;

                if (totalResult <= 0) continue;

                Journal.Log("\nCointegration arbitrage opportunity found at (DateTime.Now.ToString())");
                Journal.Log(String.Format(
                    "[{0}] {1} @ {2} --> {3} @ {4} ==> {5}",
                    edge.Home.Pair.ToGenericSymbol(),
                    edge.Home.Exchange.Name,
                    edge.Home.Orders.BestBid,
                    edge.Away.Exchange.Name,
                    edge.Home.Orders.BestAsk,
                    totalResult
                ));
            }
        }
    }
}