using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Arbitrage;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.Exchanges
{
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

        private Currency[] _currencyFilter;

        private IDisposable _mergedTradeStreamSubscription;
        private Dictionary<Market, bool> _marketComplete;
        private Dictionary<Market, List<CurrencyTrade>> _tradeBuffers;
        private object _tradeBufferLockObj;

        /// <summary>
        /// Creates a new <see cref="ExchangeNetwork"/>
        /// </summary>
        /// <param name="exchanges">Exchanges to load and add to the network</param>
        public ExchangeNetwork(Exchange[] exchanges, Currency[] currencies = null)
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
            _currencyFilter = currencies;
            _marketComplete = new Dictionary<Market, bool>();
            _tradeBuffers = new Dictionary<Market, List<CurrencyTrade>>();
            _tradeBufferLockObj = new object();
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
            for (int i = 0; i < Exchanges.Length; i++)
            {
                var exchange = Exchanges[i];
                exchange.Network = this;
                exchange.Index = i;
                exchange.Http = HttpClient;
                exchange.Symbols = new List<string>();

                foreach (var symbol in await FetchSymbols(exchange))
                {
                    if (_currencyFilter != null)
                    {
                        var pair = new CurrencyPair(exchange, symbol);
                        if (!_currencyFilter.Contains(pair.Base) || !_currencyFilter.Contains(pair.Quote))
                            continue;
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
            FetchHistoricTrades();
        }

        private static void WriteCheckmark(ConsoleColor color = ConsoleColor.Green)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔");
            Console.ResetColor();
        }

        private static void WriteFailureX()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("×");
            Console.ResetColor();
        }

        private static async Task<List<string>> FetchSymbols(Exchange exchange, int maxAttempts = 5)
        {
            Console.Write($"[{exchange.Name}] Fetching symbols... ");

            Exception exception = null;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var symbols = await exchange.FetchSymbols();
                    WriteCheckmark();
                    Thread.Sleep(350);
                    return symbols;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    WriteFailureX();
                    Thread.Sleep((2 ^ i) * 2000);
                }
            }

            throw exception;
        }

        private async void FetchHistoricTrades(int periodDuration = 60000, int periodCount = 512, int maxBackoffs = 5)
        {
            if (Program.Options.NoHistory || true) return;

            var currentMilliseconds = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
            var firstPeriodMilliseconds = Math.Ceiling(currentMilliseconds / periodDuration) * periodDuration;
            var waitMilliseconds = Math.Ceiling(firstPeriodMilliseconds - currentMilliseconds);

            Console.Write($"Waiting {(int)Math.Floor(waitMilliseconds / 1000)} seconds to fetch historical trades... ");
            await Task.Delay((int)waitMilliseconds);
            WriteCheckmark(ConsoleColor.Red);

            foreach (var exchange in Exchanges)
            {
                Console.Write($"[{exchange.Name}] Fetching historical rates... ");

                int backoffs = 0;
                Exception lastException = null;
                
                foreach (var market in exchange.Markets.Values)
                {
                    while (backoffs <= maxBackoffs)
                    {
                        try
                        {
                            var periods = await exchange.FetchTradeHistory
                            (
                                symbol: market.Symbol,
                                startTime: firstPeriodMilliseconds,
                                periodDuration: periodDuration,
                                count: periodCount
                            );

                            periods.Apply(market);
                            PlaybackBufferedTrades(market);
                            backoffs = Math.Max(backoffs - 1, 0);
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            Thread.Sleep(++backoffs ^ 2 * 2000);
                        }
                    }

                    if (lastException != null) throw lastException;
                }

                WriteCheckmark(ConsoleColor.Yellow);
            }
        }

        private void PlaybackBufferedTrades(Market market)
        {
            lock (_tradeBufferLockObj)
            {
                foreach (var trade in _tradeBuffers[market])
                    RecordTrade(trade);

                _marketComplete[market] = true;
                _tradeBuffers.Remove(market);
                
                if (_tradeBuffers.Count == 0)
                {
                    _mergedTradeStreamSubscription.Dispose();
                    _mergedTradeStreamSubscription = 
                        MergedTradeStream.Subscribe(t => RecordTrade(t));
                }
            }
        }

        private void OnConnected()
        {
            CurrencyGraph.Build();
            CurrencyGraph.RenderToImage();

            MergedOrderStream = Observable.Merge(OrderStreams);
            MergedOrderStream.Subscribe(o => RecordOrder(o));

            MergedTradeStream = Observable.Merge(TradeStreams);
            _mergedTradeStreamSubscription = (Program.Options.NoHistory || true)
                ? MergedTradeStream.Subscribe(t => RecordTrade(t))
                : MergedTradeStream.Subscribe(t => BufferOrRecordTrade(t));
                
            foreach (var exchange in Exchanges) 
            {
                Console.Write($"[{exchange.Name}] Connecting to websocket stream... ");
                exchange.Connect(exchange.Symbols);
                WriteCheckmark();
            }

            Console.Write("\nConnected to all exchanges ");
            WriteCheckmark();
        }

        private void RecordOrder(CurrencyOrder order) =>
            order.Exchange.GetMarket(order.Pair).RecordOrder(order);

        private void RecordTrade(CurrencyTrade trade)
        {
            var market = trade.Exchange.GetMarket(trade.Pair);
            market.RecordTrade(trade);
            Storage.RecordTrade(market, trade);
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
        public Market GetMarket(Exchange exchange, string symbol) => exchange.GetMarket(symbol);

        /// <summary>
        /// Retrieves an <c>OrderBook</c> based on the given <c>Exchange</c> and <c>CurrencyPair</c>
        /// </summary>
        public OrderBook GetOrderBook(Exchange exchange, CurrencyPair pair) => 
            exchange.GetMarket(pair).Orders;
    }
}