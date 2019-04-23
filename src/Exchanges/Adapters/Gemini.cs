using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Collections.Generic;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Indicators;

namespace CryptoBot.Exchanges.Adapters
{
    public struct GeminiFeedEvent {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("side")]
        public string Side;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("remaining")]
        public decimal Amount;
    }

    public struct GeminiFeedData
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("timestampms")]
        public long Time;

        [JsonProperty("events")]
        public GeminiFeedEvent[] Events;
    }

    public struct GeminiFeedPair
    {
        public WebSocketFeed<GeminiFeedData> Feed;
        public IDisposable Subscription;
    }

    public class Gemini : Exchange
    {
        public static DateTime FromTimestampMS(long ms) { return DateTime.UnixEpoch.AddMilliseconds(ms); }

        private static dynamic G_Uris = new {
            Symbols = "https://api.gemini.com/v1/symbols",
            Feed = "wss://api.gemini.com/v1/marketdata/"
        };

        private IObservable<CurrencyOrder> orderStream;
        private List<IObserver<CurrencyOrder>> observers;
        public override IObservable<CurrencyOrder> OrderStream => orderStream;
        public override IObservable<CurrencyTrade> TradeStream => throw new NotImplementedException();

        private ExchangeDetails details;
        public override ExchangeDetails Details => details;

        private Stack<string> unsubbedSymbols;
        private Dictionary<string, GeminiFeedPair> feedPairs;

        public Gemini()
        {
            details = new ExchangeDetails("Gemini", 0.01m);

            orderStream = Observable.Create((IObserver<CurrencyOrder> observer) => {
                observers.Add(observer);
                return Disposable.Empty;
            });

            observers = new List<IObserver<CurrencyOrder>>();
            feedPairs = new Dictionary<string, GeminiFeedPair>();
        }

        public override async Task<List<string>> FetchSymbols()
        {
            var response = await Http.GetAsync(G_Uris.Symbols);
            string responseBody = await response.Content.ReadAsStringAsync();

            var symbols = JsonConvert.DeserializeObject<List<string>>(responseBody);
            unsubbedSymbols = new Stack<string>(symbols);

            return symbols;
        }

        public override Task<ExchangeTradeHistory> FetchTradeHistory(string symbol, double startTime, int periodDuration, int count)
        {
            throw new NotImplementedException();
        }

        public override async void Connect(List<string> symbols)
        {
            while (unsubbedSymbols.TryPop(out string symbol))
            {
                var feed = new WebSocketFeed<GeminiFeedData>(G_Uris.Feed + symbol);
                var subscription = feed.Subscribe((message) => {
                    OnFeedUpdate(message, symbol);
                });

                feedPairs.Add(symbol, new GeminiFeedPair {
                    Feed = feed,
                    Subscription = subscription
                });

                feed.Connect();
                
                // Gemini recommends limiting 
                // connections to 3 per second
                await Task.Delay(350);
            }
        }

        public override string[] SplitSymbol(string symbol)
        {
            return new string[] { 
                symbol.Substring(0, 3),
                symbol.Substring(3, 3)
            };
        }

        private void EmitOrder(string symbol, string side, decimal price, decimal amount, long ms)
        {
            var _side = side == "buy"
                ? OrderSide.Bid
                : OrderSide.Ask;

            var order = new CurrencyOrder
            (
                exchange: this,
                symbol: symbol,
                side: _side,
                price: price,
                amount: amount,
                time: DateTime.UnixEpoch.AddMilliseconds(ms)
            );

            lock (observers)
            {
                observers.ForEach(o => o.OnNext(order));
            }
        }

        private void OnFeedUpdate(GeminiFeedData message, string symbol)
        {
            if (message.Type != "update") return;

            foreach (var change in message.Events)
            {
                EmitOrder(symbol, change.Side, change.Price, change.Amount, message.Time);
            }
        }
    }
}