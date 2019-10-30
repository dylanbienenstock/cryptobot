using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Indicators;
using System.Reactive.Subjects;

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

            [JsonProperty("filters")]
            public Dictionary<string, dynamic>[] Filters;
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

        public struct B_MarketTicker
        {
            [JsonProperty("symbol")]
            public string Symbol;

            [JsonProperty("priceChange")]
            public string PriceChange;

            [JsonProperty("priceChangePercent")]
            public string PriceChangePercentage;

            [JsonProperty("lastPrice")]
            public string LastPrice;

            [JsonProperty("volume")]
            public string Volume;
        }

        private List<WebSocketFeed<B_AggUpdate>>   _orderFeeds;
        private List<WebSocketFeed<B_AggTrade>>    _tradeFeeds;
        private List<IDisposable>                  _orderFeedSubscriptions;
        private List<IDisposable>                  _tradeFeedSubscriptions;
        private Subject<CurrencyOrder>             _orderStream;
        private Subject<CurrencyTrade>             _tradeStream;
        private ExchangeDetails                    _details;
        private Dictionary<string, B_Snapshot>     _snapshots;
        private Dictionary<string, List<B_Update>> _updateBuffer;
        private Dictionary<string, bool>           _orderbookUpToDate;
        private Dictionary<string, string[]>       _symbolDict;
        private HttpBackoffClient                  _httpClient;
        private object                             _lockObj;

        private Dictionary<(string Symbol, string Filter, string Key), dynamic> _assetFilters;

        public Binance()
        {
            _details        = new ExchangeDetails("Binance", 0.001m);
            _orderStream    = new Subject<CurrencyOrder>();
            _tradeStream    = new Subject<CurrencyTrade>();
            _snapshots      = new Dictionary<string, B_Snapshot>();
            _updateBuffer   = new Dictionary<string, List<B_Update>>();
            _symbolDict     = new Dictionary<string, string[]>();
            _httpClient     = new HttpBackoffClient("https://www.binance.com/api/v1/");
            _orderbookUpToDate = new Dictionary<string, bool>();
            _assetFilters   = new Dictionary<(string, string, string), dynamic>();
            _lockObj = new object();

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

            foreach (var symbol in response.Symbols) {
                _symbolDict.Add(symbol.Symbol, new [] { symbol.Base, symbol.Quote });
                _orderbookUpToDate[symbol.Symbol] = false;
                
                void StoreFilter(string filterType, string key)
                {
                    var keyTuple = (symbol.Symbol, filterType, key);
                    var value = symbol.Filters.First(f => (string)f["filterType"] == filterType)[key];
                    _assetFilters.Add(keyTuple, value);
                }

                StoreFilter("PRICE_FILTER",        "tickSize");
                StoreFilter("LOT_SIZE",            "stepSize");
                StoreFilter("MIN_NOTIONAL",        "minNotional");
                StoreFilter("MAX_NUM_ALGO_ORDERS", "maxNumAlgoOrders");
            }

            return symbols.ToList();
        }

        public override async Task<List<HistoricalTradingPeriod>> FetchTradingPeriods
        (
            string symbol,
            double startTime,
            long timeFrame,
            int count,
            int priority = 1
        )
        {
            var pair = new CurrencyPair(this, symbol);
            var intervalName = GetIntervalName(timeFrame);
            var endTime = startTime + (double)timeFrame * (double)count;
            var uriParams = new UriParams
            {
                { "symbol",    symbol       },
                { "interval",  intervalName },
                { "startTime", startTime    },
                { "limit",     count        }
            };

            if (count > 1) uriParams.Add("endTime", endTime);

            var response = await _httpClient.Get<decimal[][]>("klines", uriParams, priority);
            var tradingPeriods = response
                .Select(candle => new HistoricalTradingPeriod(candle))
                .ToList();

            return tradingPeriods;
        }

        public override async Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod(CurrencyPair pair)
        {
            string symbol = CurrencyPairToSymbol(pair);
            return (await FetchTradingPeriods(symbol, 0, 60000, 1, 0))[0];
        }

        public string CurrencyPairToSymbol(CurrencyPair pair) =>
            pair.ToString("").ToUpper();

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
                _orderbookUpToDate[symbol] = true;
            }

        }

        public override async void Connect(List<string> symbols)
        {
            lock (_lockObj)
            {
                _orderFeeds = new List<WebSocketFeed<B_AggUpdate>>();
                _tradeFeeds = new List<WebSocketFeed<B_AggTrade>>();
                _orderFeedSubscriptions = new List<IDisposable>();
                _tradeFeedSubscriptions = new List<IDisposable>();

                string websocketUri = "wss://stream.binance.com:9443/stream?streams=";
                int maxSymbolsPerFeed = 64;

                for (int i = 0; i < symbols.Count / (double)maxSymbolsPerFeed; i++)
                {
                    var symbolsSlice = symbols.Skip(i * maxSymbolsPerFeed).Take(maxSymbolsPerFeed);

                    string orderFeedNames = string.Join("@depth/", symbolsSlice).ToLower() + "@depth";
                    var orderFeed = new WebSocketFeed<B_AggUpdate>(websocketUri + orderFeedNames);
                    var orderFeedSub = orderFeed.Subscribe(update => EmitAggregatedUpdate(update));
                    _orderFeeds.Add(orderFeed);
                    _orderFeedSubscriptions.Add(orderFeedSub);

                    string tradeFeedNames = string.Join("@trade/", symbolsSlice).ToLower() + "@trade";
                    var tradeFeed = new WebSocketFeed<B_AggTrade>(websocketUri + tradeFeedNames);
                    var tradeFeedSub = tradeFeed.Subscribe(trade => EmitTrade(trade));
                    _tradeFeeds.Add(tradeFeed);
                    _tradeFeedSubscriptions.Add(tradeFeedSub);
                }
            }

            foreach (var orderFeed in _orderFeeds) orderFeed.Connect();
            foreach (var tradeFeed in _tradeFeeds) tradeFeed.Connect();

            SpinWait.SpinUntil(() => _orderFeeds.All(f => f.Connected) && _tradeFeeds.All(f => f.Connected));

            await GetSnapshots(symbols);
            EmitBufferedUpdates(symbols);
        }

        private void EmitTrade(B_AggTrade aggTrade)
        {
            lock (_lockObj)
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

                _tradeStream.OnNext(trade);
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

            _orderStream.OnNext(order);
        }

        private void EmitUpdate(B_Update update)
        {
            foreach (decimal[] bid in update.Bids)
                EmitOrder(update.Symbol, OrderSide.Bid, bid, update.Time);

            foreach (decimal[] ask in update.Asks)
                EmitOrder(update.Symbol, OrderSide.Ask, ask, update.Time);
        }

        private void EmitAggregatedUpdate(B_AggUpdate wrappedUpdate)
        {
            lock (_lockObj)
            {
                var update = wrappedUpdate.Data;

                if (!_orderbookUpToDate[update.Symbol])
                {
                    if (!_updateBuffer.ContainsKey(update.Symbol))
                        _updateBuffer[update.Symbol] = new List<B_Update>();

                    _updateBuffer[update.Symbol].Add(update);
                    return;
                }

                EmitUpdate(update);
            }
        }

        private void EmitSnapshot(B_Snapshot snapshot, string symbol)
        {
            foreach (decimal[] bid in snapshot.Bids)
                EmitOrder(symbol, OrderSide.Bid, bid, 0);
            
            foreach (decimal[] ask in snapshot.Asks)
                EmitOrder(symbol, OrderSide.Ask, ask, 0);
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

        public override async Task<MarketTicker[]> GetMarketTickers()
        {
            var response = await _httpClient.Get<B_MarketTicker[]>("ticker/24hr", null, -1);

            return response.Select(mt => {
                var market = GetMarket(mt.Symbol);
                return new MarketTicker
                (
                    market:                market,
                    priceChange:           mt.PriceChange,
                    priceChangePercentage: mt.PriceChangePercentage,
                    lastPrice:             mt.LastPrice,
                    volume:                mt.Volume
                );
            })
            .ToArray();
        }
    }
}