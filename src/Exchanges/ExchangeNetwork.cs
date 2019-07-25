using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Arbitrage;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Indicators;
using CryptoBot.Storage;

namespace CryptoBot.Exchanges
{
    public enum CurrencyFilter
    {
        Either,
        Both,
        Neither
    }

    /// <summary>
    /// A collection of <see cref="Exchange"/> objects.
    /// * Gets all available symbols from each exchange and compiles them into a <see cref="Arbitrage.MarketGraph"/> object.
    /// * Subscribes all exchanges to their respective websocket feeds, and merges their outputs into a single observable.
    /// </summary>
    public class ExchangeNetwork
    {
        public HttpClient HttpClient;

        /// <summary>
        /// All exchanges loaded through the constructor or AddExchanges()
        /// </summary>
        public Exchange[] Exchanges;

        public CurrencyGraph CurrencyGraph;

        /// <summary>
        /// A merged observable built from each exchange's order stream
        /// </summary>
        public IObservable<CurrencyOrder> MergedOrderStream;

        /// <summary>
        /// A list of each exchange's order steam observable
        /// </summary>
        public List<IObservable<CurrencyOrder>> OrderStreams;
    
        /// <summary>
        /// A merged observable built from each exchange's trade stream
        /// </summary>
        public IObservable<CurrencyTrade> MergedTradeStream;

        /// <summary>
        /// A list of each exchange's trade steam observable
        /// </summary>
        public List<IObservable<CurrencyTrade>> TradeStreams;

        /// <summary>
        /// Manages an instance of each indicator for each market
        /// </summary>
        public IndicatorManifold Indicators;

        public Subject<SyncStatusUpdate> SyncStatus;

        private Currency[] _currencyFilter;
        private CurrencyFilter _currencyFilterMode;

        private IDisposable _mergedTradeStreamSubscription;
        private Dictionary<Market, bool> _marketComplete;
        private Dictionary<Market, List<CurrencyTrade>> _tradeBuffers;
        private object _tradeBufferLockObj;

        private bool _connectWasCalled = false;

        /// <summary>
        /// Creates a new <see cref="ExchangeNetwork"/>
        /// </summary>
        /// <param name="exchanges">Exchanges to load and add to the network</param>
        public ExchangeNetwork(Exchange[] exchanges, CurrencyFilter filter = CurrencyFilter.Either, Currency[] currencies = null)
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", @"
                Mozilla/5.0 (Windows NT 10.0; Win64; x64) 
                AppleWebKit/537.36 (KHTML, like Gecko) 
                Chrome/71.0.3578.80 Safari/537.36
            ".Replace("\n", ""));

            Exchanges = exchanges;
            CurrencyGraph = new CurrencyGraph(this);
            OrderStreams = new List<IObservable<CurrencyOrder>>();
            TradeStreams = new List<IObservable<CurrencyTrade>>();
            SyncStatus = new Subject<SyncStatusUpdate>();
            _currencyFilter = currencies;
            _currencyFilterMode = filter;
            _marketComplete = new Dictionary<Market, bool>();
            _tradeBuffers = new Dictionary<Market, List<CurrencyTrade>>();
            _tradeBufferLockObj = new object();

            // MergedOrderStream = new Observable<CurrencyOrder>();
            // MergedOrderStream.Subscribe(o => RecordOrder(o));
        }

        /// <summary>
        /// Adds exchanges to the network
        /// * Adds them to <c>Exchanges</c> 
        /// * Fetches their symbols
        /// * Connects and subscribes to their websocket feeds
        /// * Merges their websocket feeds (as <c>Exchange.OrderStream</c>) into <c>MergedOrderStream</c>
        /// </summary>
        /// <param name="exchanges">Exchanges to load and add to the network</param>
        public async Task Connect()
        {
            if (_connectWasCalled) return;

            for (int i = 0; i < Exchanges.Length; i++)
            {
                var exchange = Exchanges[i];
                exchange.OrderStream.Subscribe(o => RecordOrder(o));
                exchange.Network = this;
                exchange.Index = i;
                exchange.Http = HttpClient;
                exchange.Symbols = new List<string>();

                foreach (var symbol in await FetchSymbols(exchange))
                {
                    if (_currencyFilter != null)
                    {
                        var pair = new CurrencyPair(exchange, symbol);
                        bool allowBase = _currencyFilter.Contains(pair.Base);
                        bool allowQuote = _currencyFilter.Contains(pair.Quote);
                        bool allowPair = false;

                        switch (_currencyFilterMode)
                        {
                            case CurrencyFilter.Either:
                                allowPair = allowBase || allowQuote;
                                break;
                            case CurrencyFilter.Both:
                                allowPair = allowBase && allowQuote;
                                break;
                            case CurrencyFilter.Neither:
                                allowPair = !allowBase && !allowQuote;
                                break;
                        }

                        if (!allowPair) continue;
                    }

                    var market = new Market(exchange, symbol);
                    exchange.Symbols.Add(symbol);
                    exchange.AddMarket(market);

                    lock (_tradeBufferLockObj)
                    {
                        _marketComplete[market] = false;
                        _tradeBuffers[market] = new List<CurrencyTrade>();
                    }
                }

                OrderStreams.Add(exchange.OrderStream);
                TradeStreams.Add(exchange.TradeStream);
            }

            OnConnected();
        }

        private static async Task<List<string>> FetchSymbols(Exchange exchange, int maxAttempts = 5) =>
            await exchange.FetchSymbols();

        public async Task<List<HistoricalTradingPeriod>> GetTradingPeriods(Market market, double startTime, double endTime, long timeFrame)
        {
            var periods = await market.Exchange.FetchTradingPeriods(market, startTime, endTime, timeFrame);

            return periods
                .Distinct()
                .OrderBy(t => t.Minute)
                .ToList();
        }

        private void PlaybackBufferedTrades(Market market)
        {
            lock (_tradeBufferLockObj)
            {
                foreach (var trade in _tradeBuffers[market])
                    RecordTrade(trade);

                _marketComplete[market] = true;
                _tradeBuffers.Remove(market);
                
                if (_tradeBuffers.Count > 0) return;
            
                _mergedTradeStreamSubscription.Dispose();
                _mergedTradeStreamSubscription = 
                    MergedTradeStream.Subscribe(t => RecordTrade(t));
            }
        }

        private void OnConnected()
        {
            Indicators = new IndicatorManifold(this);

            CurrencyGraph.Build();
            CurrencyGraph.RenderToImage();

            MergedOrderStream = Observable.Merge(OrderStreams);
            // MergedOrderStream.Subscribe(o => RecordOrder(o));

            MergedTradeStream = Observable.Merge(TradeStreams);
            _mergedTradeStreamSubscription =
                MergedTradeStream.Subscribe(t => BufferOrRecordTrade(t));
                
            foreach (var exchange in Exchanges) 
                exchange.Connect(exchange.Symbols);
        }

        private void RecordOrder(CurrencyOrder order)
        {
            var market = order.Exchange.GetMarket(order.Pair);
            market.RecordOrder(order);
        }

        private void RecordTrade(CurrencyTrade trade)
        {
            var market = trade.Exchange.GetMarket(trade.Pair);
            market.RecordTrade(trade);
        }

        private void BufferOrRecordTrade(CurrencyTrade trade)
        {
            var market = trade.Exchange.GetMarket(trade.Pair);

            lock (_tradeBufferLockObj)
            {
                if (!_marketComplete[market])
                {
                    _tradeBuffers[market].Add(trade);
                    return;
                }                
            }

            RecordTrade(trade);
        }

        public Exchange GetExchange(string name) => Exchanges.First(e => e.Name == name);

        public Market GetMarket(Exchange exchange, CurrencyPair pair) => exchange.GetMarket(pair);
        public Market GetMarket(Exchange exchange, string symbol)     => exchange.GetMarket(symbol);
        public Market GetMarket(string name, string symbol)           => GetExchange(name).GetMarket(symbol);
        public Market GetMarket(string name, CurrencyPair pair)       => GetExchange(name).GetMarket(pair);
        public Market GetMarket(string name, string symbol, bool generic) =>
            generic ? GetExchange(name).GetMarket(CurrencyPair.FromGenericSymbol(symbol))
                    : GetExchange(name).GetMarket(symbol);

        /// <summary>
        /// Retrieves an <c>OrderBook</c> based on the given <c>Exchange</c> and <c>CurrencyPair</c>
        /// </summary>
        public OrderBook GetOrderBook(Exchange exchange, CurrencyPair pair) => 
            exchange.GetMarket(pair).Orders;
    }
}