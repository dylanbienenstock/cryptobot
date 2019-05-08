using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Indicators;

namespace CryptoBot.Exchanges
{
    public class Binance : Exchange
    {
        public struct B_Symbol
        {
            [JsonProperty("symbol")]
            public string Symbol;

            [JsonProperty("baseAsset")]
            public string Base;

            [JsonProperty("quoteAsset")]
            public string Quote;
        }

        public struct B_ExchangeInfo
        {
            [JsonProperty("symbols")]
            public B_Symbol[] Symbols;
        }

        public struct B_Snapshot
        {
            [JsonProperty("lastUpdateId")]
            public int LastUpdateId;

            [JsonProperty("bids")]
            public decimal[][] Bids;

            [JsonProperty("asks")]
            public decimal[][] Asks;
        }

        public struct B_Update
        {
            [JsonProperty("e")]
            public string Type;

            [JsonProperty("E")]
            public long Time;

            [JsonProperty("s")]
            public string Symbol;

            [JsonProperty("u")]
            public int LastUpdateId;

            [JsonProperty("b")]
            public decimal[][] Bids;

            [JsonProperty("a")]
            public decimal[][] Asks;
        }

        public struct B_Trade
        {
            [JsonProperty("t")]
            public int Id;

            [JsonProperty("s")]
            public string Symbol;

            [JsonProperty("p")]
            public decimal Price;

            [JsonProperty("q")]
            public decimal Amount;

            [JsonProperty("T")]
            public long Time;

            [JsonProperty("m")]
            public bool BuyerIsMaker;
        }

        public struct B_AggUpdate
        {
            [JsonProperty("stream")]
            public string StreamName;

            [JsonProperty("data")]
            public B_Update Data;
        }

        public struct B_AggTrade
        {
            [JsonProperty("stream")]
            public string StreamName;

            [JsonProperty("data")]
            public B_Trade Data;
        }

        private WebSocketFeed<B_AggUpdate>         _orderFeed;
        private WebSocketFeed<B_AggTrade>          _tradeFeed;
        private IDisposable                        _orderFeedSubscription;
        private IDisposable                        _tradeFeedSubscription;
        private IObservable<CurrencyOrder>         _orderStream;
        private IObservable<CurrencyTrade>         _tradeStream;
        private List<IObserver<CurrencyOrder>>     _orderObservers;
        private List<IObserver<CurrencyTrade>>     _tradeObservers;
        private ExchangeDetails                    _details;
        private Dictionary<string, B_Snapshot>     _snapshots;
        private Dictionary<string, List<B_Update>> _updateBuffer;
        private Dictionary<string, string[]>       _symbolDict;
        private HttpBackoffClient                  _httpClient;
        private bool                               _fullyConnected;

        public Binance()
        {
            _details        = new ExchangeDetails("Binance", 0.001m);
            _orderStream    = Observable.Create((IObserver<CurrencyOrder> o) => OnOrderStreamSubscribed(o));
            _tradeStream    = Observable.Create((IObserver<CurrencyTrade> t) => OnTradeStreamSubscribed(t));
            _orderObservers = new List<IObserver<CurrencyOrder>>();
            _tradeObservers = new List<IObserver<CurrencyTrade>>();
            _snapshots      = new Dictionary<string, B_Snapshot>();
            _updateBuffer   = new Dictionary<string, List<B_Update>>();
            _symbolDict     = new Dictionary<string, string[]>();
            _httpClient     = new HttpBackoffClient("https://www.binance.com/api/v1/");
            _fullyConnected = false;

            _httpClient.SetBackoff((attempts, response) =>
            {
                if (response == null) return null;

                int statusCode = (int)response.StatusCode;

                if (statusCode == 418 || statusCode == 429)
                {
                    string delayHeader = response.Headers.GetValues("Retry-After").First();
                    return int.Parse(delayHeader) * 1000 + 500;
                }

                return null;
            });
        }

        public override IObservable<CurrencyOrder> OrderStream => _orderStream;
        public override IObservable<CurrencyTrade> TradeStream => _tradeStream;
        public override ExchangeDetails Details => _details;

        public override string[] SplitSymbol(string symbol) => _symbolDict[symbol];

        public override async Task<List<string>> FetchSymbols()
        {
            B_ExchangeInfo response = await _httpClient.Get<B_ExchangeInfo>("exchangeInfo");
            var symbols = response.Symbols.Select(s => s.Symbol);

            foreach (var symbol in response.Symbols)
                _symbolDict.Add(symbol.Symbol, new [] { symbol.Base, symbol.Quote });

            return symbols.ToList();
        }

        public override async Task<List<HistoricalTradingPeriod>> FetchHistoricalTradingPeriods
        (
            string symbol,
            double start,
            int interval,
            int count
        )
        {
            if (count > 1000) throw new Exception("Cannot fetch more than 1000 candles");

            var pair = new CurrencyPair(this, symbol);
            var intervalName = GetIntervalName(interval);
            var tradingPeriods = (await _httpClient.Get<decimal[][]>
            (
                endpoint: "klines",
                parameters: new UriParams
                {
                    { "symbol",    symbol        },
                    { "interval",  intervalName  },
                    { "limit",     count         },
                    { "startTime", (Int64)start  }
                }
            ))
            .Select(candle => new HistoricalTradingPeriod(candle))
            .ToList();

            return tradingPeriods;
        }

        public override async Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod(CurrencyPair pair)
        {
            string symbol = CurrencyPairToSymbol(pair);
            return (await FetchHistoricalTradingPeriods(symbol, 0, 60000, 1))[0];
        }

        public string CurrencyPairToSymbol(CurrencyPair pair) =>
            pair.ToString("").ToUpper();

        private IDisposable OnOrderStreamSubscribed(IObserver<CurrencyOrder> observer) {
            _orderObservers.Add(observer);
            return Disposable.Empty;
        }

        private IDisposable OnTradeStreamSubscribed(IObserver<CurrencyTrade> observer) {
            _tradeObservers.Add(observer);
            return Disposable.Empty;
        }

        private async Task GetSnapshots(List<string> symbols)
        {
            foreach (string symbol in symbols)
            {
                var snapshot = await _httpClient.Get<B_Snapshot>
                (
                    endpoint: "depth",
                    parameters: new UriParams
                    {
                        { "symbol", symbol }
                    }
                );

                _snapshots[symbol] = snapshot;
                EmitSnapshot(snapshot, symbol);
            }

            _fullyConnected = true;
        }

        public override async void Connect(List<string> symbols)
        {
            string websocketUri = "wss://stream.binance.com:9443/stream?streams=";

            string orderFeedNames = string.Join("@depth/", symbols).ToLower() + "@depth";
            _orderFeed = new WebSocketFeed<B_AggUpdate>(websocketUri + orderFeedNames);
            _orderFeedSubscription = _orderFeed.Subscribe(EmitAggregatedUpdate);

            string tradeFeedNames = string.Join("@trade/", symbols).ToLower() + "@trade";
            _tradeFeed = new WebSocketFeed<B_AggTrade>(websocketUri + tradeFeedNames);
            _tradeFeedSubscription = _tradeFeed.Subscribe(EmitTrade);

            _tradeFeed.Connect();
            _orderFeed.Connect();
            await GetSnapshots(symbols);
            EmitBufferedUpdates(symbols);
        }

        private void EmitTrade(B_AggTrade aggTrade)
        {
            var side = aggTrade.Data.BuyerIsMaker ? OrderSide.Bid : OrderSide.Ask;
            var time = DateTime.UnixEpoch.AddMilliseconds(aggTrade.Data.Time);
            var trade = new CurrencyTrade
            (
                exchange: this,
                symbol:   aggTrade.Data.Symbol,
                id:       aggTrade.Data.Id,
                side:     side,
                price:    aggTrade.Data.Price,
                amount:   aggTrade.Data.Amount,
                time:     time
            );

            lock (_tradeObservers)
            {
                _tradeObservers.ForEach(o => o.OnNext(trade));
            }
        }

        private void EmitOrder(string symbol, OrderSide side, decimal[] orderTuple, long ms)
        {
            var time = DateTime.UnixEpoch.AddMilliseconds(ms);
            var order = new CurrencyOrder
            (
                exchange: this,
                symbol:   symbol,
                side:     side,
                price:    orderTuple[0],
                amount:   orderTuple[1],
                time:     time
            );

            lock (_orderObservers)
            {
                _orderObservers.ForEach(o => o.OnNext(order));
            }
        }

        private void EmitUpdate(B_Update update)
        {
            foreach (decimal[] bid in update.Bids)
                this.EmitOrder(update.Symbol, OrderSide.Bid, bid, update.Time);

            foreach (decimal[] ask in update.Asks)
                this.EmitOrder(update.Symbol, OrderSide.Ask, ask, update.Time);
        }

        private void EmitAggregatedUpdate(B_AggUpdate wrappedUpdate)
        {
            var update = wrappedUpdate.Data;
            
            if (!_fullyConnected)
            {
                if (!_updateBuffer.ContainsKey(update.Symbol))
                    _updateBuffer[update.Symbol] = new List<B_Update>();

                _updateBuffer[update.Symbol].Add(update);
                return;
            }

            EmitUpdate(update);
        }

        private void EmitSnapshot(B_Snapshot snapshot, string symbol)
        {
            foreach (decimal[] bid in snapshot.Bids)
                this.EmitOrder(symbol, OrderSide.Bid, bid, 0);
            
            foreach (decimal[] ask in snapshot.Asks)
                this.EmitOrder(symbol, OrderSide.Ask, ask, 0);
        }

        private void EmitBufferedUpdates(List<string> symbols)
        {
            foreach (string symbol in symbols)
            {
                if (!_updateBuffer.ContainsKey(symbol)) continue;

                var snapshot = _snapshots[symbol];

                foreach (var update in _updateBuffer[symbol])
                {
                    if (update.LastUpdateId > snapshot.LastUpdateId)
                        EmitUpdate(update);
                }
            }
        }
    }
}