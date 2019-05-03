using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Indicators;
using System.Net;
using System.Threading;

namespace CryptoBot.Exchanges.Adapters
{
    public class CoinbasePro : Exchange
    {
        /// <summary>
        /// Rules for decimal parsing, Coinbase Pro occasionally sends values in exponential notation
        /// </summary>
        public readonly NumberStyles CB_NumStyle =
            NumberStyles.AllowExponent |
            NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Rules for decimal parsing, use period instead of comma to denote decimals
        /// </summary>
        public readonly CultureInfo CB_NumCult =
            CultureInfo.InvariantCulture;

        /// <summary>
        /// Response from Coinbase Pro's product info endpoint
        /// </summary>
        public struct ProductInfo
        {
            [JsonProperty("id")]
            public string Symbol;
        }

        /// <summary>
        /// Response from Coinbase Pro's product trades endpoint
        /// </summary>
        public struct Trade
        {
            [JsonProperty("time")]
            public DateTime Time;

            [JsonProperty("trade_id")]
            public int Trade_TradeId;

            [JsonProperty("price")]
            public decimal Price;

            [JsonProperty("size")]
            public decimal Amount;

            [JsonProperty("side")]
            public string Side;
        }

        /// <summary>
        /// Coinbase Pro's websocket feed data format
        /// * TODO: Split this into multiple structs
        /// </summary>
        public struct FeedData
        {
            [JsonProperty("type")]
            public string Type;

            [JsonProperty("product_id")]
            public string Symbol;

            [JsonProperty("time")]
            public DateTime Time;

            #region Match
            [JsonProperty("trade_id")]
            public int Trade_Id;

            [JsonProperty("price")]
            public decimal Trade_Price;

            [JsonProperty("size")]
            public decimal Trade_Amount;

            [JsonProperty("side")]
            public string Trade_Side;
            #endregion

            #region Snapshot
            [JsonProperty("bids")]
            public decimal[][] Snapshot_Bids;

            [JsonProperty("asks")]
            public decimal[][] Snapshot_Asks;
            #endregion

            #region Update
            [JsonProperty("changes")]
            public string[][] Update_Changes;
            #endregion
        }

        private static string CB_BaseUri = "https://api.pro.coinbase.com";
        private static dynamic uris = new
        {
            Products = CB_BaseUri + "/products",
            Websocket = "wss://ws-feed.pro.coinbase.com"
        };

        private ExchangeDetails                    _details;
        private IObservable<CurrencyOrder>         _orderStream;
        private IObservable<CurrencyTrade>         _tradeStream;
        private List<IObserver<CurrencyOrder>>     _orderObservers;
        private List<IObserver<CurrencyTrade>>     _tradeObservers;
        private WebSocketFeed<FeedData>            _feed;
        private IDisposable                        _feedSub;
        private Dictionary<string, int>            _lastTradeIds;
        private Dictionary<string, List<FeedData>> _tradeBuffers;

        public override IObservable<CurrencyOrder> OrderStream => _orderStream;
        public override IObservable<CurrencyTrade> TradeStream => _tradeStream;
        public override ExchangeDetails            Details     => _details;

        public CoinbasePro()
        {
            _details        = new ExchangeDetails("Coinbase Pro", 0.0025m);
            _orderStream    = Observable.Create((IObserver<CurrencyOrder> o) => OnOrderStreamSub(o));
            _tradeStream    = Observable.Create((IObserver<CurrencyTrade> o) => OnTradeStreamSub(o));
            _orderObservers = new List<IObserver<CurrencyOrder>>();
            _tradeObservers = new List<IObserver<CurrencyTrade>>();
            _feed           = new WebSocketFeed<FeedData>(uris.Websocket);
            _lastTradeIds   = new Dictionary<string, int>();
            _tradeBuffers   = new Dictionary<string, List<FeedData>>();
        }

        public override async Task<List<string>> FetchSymbols()
        {
            var response = await Http.GetAsync(uris.Products);
            var responseBody = await response.Content.ReadAsStringAsync();
            var products = JsonConvert.DeserializeObject<ProductInfo[]>((string)responseBody);
            var symbols = products.Select(p => p.Symbol);

            return symbols.ToList();
        }

        /// <summary>
        /// Pulls historic trading periods from the REST API, seeking 
        /// backwards in time from the start of the next trading period.
        /// </summary>
        /// <param name="symbol">
        /// Which symbol to pull data for
        /// </param>
        /// <param name="periodDuration">
        /// The granularity of the data to request, in milliseconds
        /// </param>
        /// <param name="count">
        /// How many trading periods to pull
        /// </param>
        /// <returns></returns>
        public override async Task<ExchangeTradeHistory> FetchTradeHistory(string symbol, double startTime, int periodDuration, int count)
        {
            // A maximum of 300 periods can be requested at one time
            var allPeriods = new List<TradingPeriod>();
            int requestCount = (int)Math.Ceiling(count / 300f);
            int backoffs = 0;

            for (int i = 0; i < requestCount; i++)
            {
                // Coinbase Pro allows a maximum of 3 requests/second
                // More info: https://docs.pro.coinbase.com/#rate-limits
                await Task.Delay(350);

                // Determine the first and last periods to request
                var inverseI    = (requestCount - 1) - i;
                var periodDepth = inverseI < (requestCount - 1) ? (inverseI + 1) * 300 : inverseI * 300 + count % 300;
                var periodCount = Math.Min(count - inverseI * 300, 300);

                // Convert them to millisecond amounts relative to startTime
                var seekOrigin  = startTime - periodDuration * periodDepth;
                var seekEnd     = seekOrigin + periodDuration * periodCount;

                // Timestamps should conform to ISO 8601
                // More info: https://en.wikipedia.org/wiki/ISO_8601
                var start     = DateTime.UnixEpoch.AddMilliseconds(seekOrigin);
                var end       = DateTime.UnixEpoch.AddMilliseconds(seekEnd);
                var uriParams = String.Join('&', new string[]
                {
                    "start="       + start.ToString("s"),
                    "end="         + end.ToString("s"),
                    "granularity=" + periodDuration / 1000
                });
                var uri = $"{uris.Products}/{symbol}/candles?{uriParams}";

                HttpResponseMessage response = null;
                Exception lastException = null;

                while (backoffs < 5)
                {
                    try
                    {
                        response = await Http.GetAsync(uri);

                        if ((int)response.StatusCode != 200) throw new Exception
                        (
                            "[Coinbase Pro | FetchHistoricRates] Received non-success status code: " +
                            $"{(int)response.StatusCode} {Enum.GetName(typeof(HttpStatusCode), response.StatusCode)}"
                        );

                        if (response == null) throw lastException;

                        var responseBody = await response.Content.ReadAsStringAsync();
                        var rawPeriods = JsonConvert.DeserializeObject<decimal[][]>((string)responseBody);
                        var periods = rawPeriods.Select(rawPeriod => new TradingPeriod
                        {
                            Time  = DateTime.UnixEpoch.AddSeconds((double)rawPeriod[0] /* - periodDuration / 1000 */),
                            Low   = rawPeriod[1],
                            High  = rawPeriod[2],
                            Open  = rawPeriod[3],
                            Close = rawPeriod[4]
                        });

                        allPeriods.AddRange(periods);
                        backoffs = Math.Max(backoffs - 1, 0);
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        await Task.Delay(++backoffs ^ 2 * 2000);
                    }
                }

                if (response == null) throw lastException;
            }

            return new ExchangeTradeHistory
            (
                market => allPeriods
                    .Distinct()
                    .OrderBy(period => period.Time)
                    .ToList()
                    .ForEach(period => market.TradingPeriods.Add(period))
            );
        }

        private IDisposable OnOrderStreamSub(IObserver<CurrencyOrder> observer)
        {
            lock (_orderObservers)
            {
                _orderObservers.Add(observer);
            }

            return Disposable.Empty;
        }

        private IDisposable OnTradeStreamSub(IObserver<CurrencyTrade> observer)
        {
            lock (_tradeObservers)
            {
                _tradeObservers.Add(observer);
            }

            return Disposable.Empty;
        }

        public override void Connect(List<string> symbols)
        {
            foreach (var symbol in symbols)
                _lastTradeIds[symbol] = 0;

            _feedSub = _feed.Subscribe(OnFeedData);

            _feed.Connect(new
            {
                type = "subscribe",
                product_ids = symbols,
                channels = new string[]
                {
                    "matches",
                    "level2"
                }
            });
        }

        public override string[] SplitSymbol(string pair)
        {
            return pair.Split("-");
        }

        private void EmitOrder(string symbol, OrderSide side, decimal price, decimal amount, DateTime time)
        {
            var order = new CurrencyOrder
            (
                exchange: this,
                symbol: symbol,
                side: side,
                price: price,
                amount: amount,
                time: time
            );

            lock (_orderObservers)
            {
                _orderObservers.ForEach(o => o.OnNext(order));
            }
        }

        private void EmitTrade(string symbol, int id, OrderSide side, decimal price, decimal amount, DateTime time) {
            var trade = new CurrencyTrade
            (
                exchange: this,
                symbol: symbol,
                id: id,
                side: side,
                price: price,
                amount: amount,
                time: time
            );

            lock (_tradeObservers)
            {
                _tradeObservers.ForEach(o => o.OnNext(trade));
            }
        }

        private void OnFeedData(FeedData data)
        {
            switch (data.Type)
            {
                case "match": OnTrade(data); return;
                case "snapshot": EmitSnapshot(data); return;
                case "l2update": EmitUpdate(data); return;
            }
        }

        private void OnTrade(FeedData data, bool verifySequence = true)
        {
            if (verifySequence)
            {
                int lastTradeId = _lastTradeIds[data.Symbol];
                _lastTradeIds[data.Symbol] = data.Trade_Id;

                if (lastTradeId != 0 && data.Trade_Id != lastTradeId + 1)
                {
                    RecoverDroppedTrades(
                        symbol: data.Symbol,
                        after: lastTradeId,
                        before: data.Trade_Id
                    );
                }
            }

            if (!_tradeBuffers.ContainsKey(data.Symbol))
            {
                var side = data.Trade_Side == "buy"
                    ? OrderSide.Bid
                    : OrderSide.Ask;

                EmitTrade(data.Symbol, data.Trade_Id, side, data.Trade_Price, data.Trade_Amount, data.Time);

                return;
            }

            _tradeBuffers[data.Symbol].Add(data);
        }

        private async void RecoverDroppedTrades(string symbol, int after, int before)
        {
            string uri = uris.Products + "/";
            uri += symbol + "/trades";
            uri += "?after=" + after;
            uri += "&before=" + before;

            var response = await Http.GetAsync(uri);
            string responseBody = await response.Content.ReadAsStringAsync();
            var recoveredTrades = JsonConvert.DeserializeObject<Trade[]>(responseBody);

            if (!_tradeBuffers.ContainsKey(symbol))
                _tradeBuffers[symbol] = new List<FeedData>();

            foreach (var bufferedTrade in _tradeBuffers[symbol])
                OnTrade(bufferedTrade, false);

            for (int i = 0; i < recoveredTrades.Length; i++)
            {
                var recoveredTrade = recoveredTrades[i];

                OnTrade(new FeedData
                {
                    Symbol = symbol,
                    Trade_Side = recoveredTrade.Side,
                    Trade_Price = recoveredTrade.Price,
                    Trade_Amount = recoveredTrade.Amount,
                    Time = recoveredTrade.Time
                }, false);
            }

            _tradeBuffers[symbol] = null;
        }

        private void EmitSnapshot(FeedData data)
        {
            var now = DateTime.Now;

            foreach (var bid in data.Snapshot_Bids)
                EmitOrder(data.Symbol, OrderSide.Bid, bid[0], bid[1], data.Time);

            foreach (var ask in data.Snapshot_Asks)
                EmitOrder(data.Symbol, OrderSide.Ask, ask[0], ask[1], data.Time);
        }

        private void EmitUpdate(FeedData data)
        {
            foreach (var change in data.Update_Changes)
            {
                OrderSide side = change[0] == "buy"
                    ? OrderSide.Bid
                    : OrderSide.Ask;

                if (!Decimal.TryParse(change[1], CB_NumStyle, CB_NumCult, out decimal price))
                    throw new Exception("Failed to parse price");

                if (!Decimal.TryParse(change[2], CB_NumStyle, CB_NumCult, out decimal amount))
                    throw new Exception("Failed to parse amount");

                EmitOrder(data.Symbol, (OrderSide)side, price, amount, data.Time);
            }
        }
    }
}