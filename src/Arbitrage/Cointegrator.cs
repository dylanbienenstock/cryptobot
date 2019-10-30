using System;
using System.Collections.Generic;
using System.Threading;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators;

namespace CryptoBot.Arbitrage
{
    /// <summary>
    /// Cointegration arbitrage opportunity detector
    /// </summary>
    public class Cointegrator
    {
        /// <summary>
        /// Determines how long to hold reverse profitablity history
        /// in <see cref="CointegrableEdge.ReverseHistory"/>
        /// </summary>
        public static TimeSpan HistoryTimespan = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Frequency to scan for arbitrage opportunities
        /// </summary>
        public long Frequency;

        /// <summary>
        /// <see cref="ExchangeNetwork"/> to run on
        /// </summary>
        private ExchangeNetwork _network;

        /// <summary>
        /// Edges between all tracked markets
        /// </summary>
        private List<CointegrableEdge> _edges;

        /// <summary>
        /// Execution thread
        /// </summary>
        private Thread _thread;

        /// <summary>
        /// Constructs a new <see cref="Cointegrator"/>
        /// </summary>
        /// <param name="network">
        /// <see cref="ExchangeNetwork"/> to run on
        /// </param>
        /// <param name="frequency">
        /// Frequency to scan for arbitrage opportunities
        /// </param>
        public Cointegrator(ExchangeNetwork network, long frequency = 1)
        {
            _network = network;
            _edges = new List<CointegrableEdge>();
            _thread = new Thread(Start);
            _thread.Start();
        }

        /// <summary>
        /// Builds the graph and starts the execution thread
        /// </summary>
        public void Start()
        {
            BuildGraph();

            while (true)
            {
                TestMarkets();
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Builds a graph of <see cref="CointegrableEdge"/> instances
        /// </summary>
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

        /// <summary>
        /// Gets the profitability of moving funds from one market to another
        /// </summary>
        /// <param name="from">
        /// Market with funds
        /// </param>
        /// <param name="to">
        /// Market to move funds to
        /// </param>
        /// <param name="withFees">
        /// Enable or disable fee consideration
        /// </param>
        /// <returns>Decimal fraction representing profitability %</returns>
        public decimal GetTransactionResult(Market from, Market to, bool withFees = true)
        {
            if (withFees) return
              from.BestBid * (1 - from.Exchange.Fee)
              - to.BestAsk * (1 + to.Exchange.Fee);

            return from.BestBid - to.BestAsk;
        }

        /// <summary>
        /// Finds cointegration arbitrage opportunities by
        /// scanning for discrepencies in exchange rates for the
        /// same pair across different exchanges
        /// </summary>
        private void TestMarkets()
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