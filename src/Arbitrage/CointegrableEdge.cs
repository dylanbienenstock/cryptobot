using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators;

namespace CryptoBot.Arbitrage {
    /// <summary>
    /// Represents an edge between two cointegrable markets
    /// </summary>
    public struct CointegrableEdge
    {
        /// <summary>
        /// The home (origin) market
        /// </summary>
        public Market Home;

        /// <summary>
        /// The away (destination) market
        /// </summary>
        public Market Away;

        /// <summary>
        /// History of profitability moving funds from
        /// Away market to Home market
        /// </summary>
        public TimeSeries<decimal> ReverseHistory;

        /// <summary>
        /// Simple moving average of ReverseHistory
        /// </summary>
        public SimpleMovingAverage ReverseSMA;

        /// <summary>
        /// Constructs a new <see cref="CointegrableEdge"/>
        /// </summary>
        /// <param name="home">
        /// The home (origin) market
        /// </param>
        /// <param name="away">
        /// The away (destination) market
        /// </param>
        public CointegrableEdge(Market home, Market away)
        {
            Home = home;
            Away = away;
            ReverseHistory = new TimeSeries<decimal>(Cointegrator.HistoryTimespan);
            ReverseSMA = new SimpleMovingAverage(ReverseHistory);
        }
    }
}