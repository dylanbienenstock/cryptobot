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

        private static dynamic B_Uris = new {
            Symbols = "https://api.binance.com/api/v1/exchangeInfo",
            Snapshot = "https://www.binance.com/api/v1/depth?symbol=",
            Websocket = "wss://stream.binance.com:9443/stream?streams="
        };

        private B_OrderFeed _orderFeed;
        private IDisposable     _orderFeedSubscription;
        private B_Observable    _OrderStream;
        private B_ObserverList  _observers;
        private bool            _fullyConnected;
        private ExchangeDetails _Details;
        private B_SnapshotDict  _snapshots;
        private B_UpdateBuffer  _updateBuffer;
        private B_SymbolDict    _symbolDict;

        public Binance()
        {
            _Details       = new ExchangeDetails("Binance", 0.001m);
            _OrderStream   = Observable.Create((B_Observer o) => OnSubscribe(o));
            _fullyConnected = false;
            _observers      = new B_ObserverList();
            _snapshots      = new B_SnapshotDict();
            _updateBuffer   = new B_UpdateBuffer();
            _symbolDict     = new B_SymbolDict();
        }

        public override IObservable<CurrencyOrder> OrderStream => _OrderStream;
        public override IObservable<CurrencyTrade> TradeStream => throw new NotImplementedException();
        public override ExchangeDetails Details => _Details;

        public override string[] SplitSymbol(string symbol) => _symbolDict[symbol];

        public override async Task<List<string>> FetchSymbols()
        {
            var response = await Http.GetAsync(B_Uris.Symbols);
            string responseBody = await response.Content.ReadAsStringAsync();

            var symbolsResponse = JsonConvert.DeserializeObject<B_ExchangeInfo>(responseBody);
            var symbols = symbolsResponse.Symbols.Select(s => s.Symbol);

            foreach (var symbol in symbolsResponse.Symbols)
                _symbolDict.Add(symbol.Symbol, new string[2] { symbol.Base, symbol.Quote });

            return symbols.ToList();
        }

        public override Task<ExchangeTradeHistory> FetchTradeHistory(string symbol, double startTime, int periodDuration, int count)
        {
            throw new NotImplementedException();
        }

        private IDisposable OnSubscribe(IObserver<CurrencyOrder> observer) {
            _observers.Add(observer);
            return Disposable.Empty;
        }

        private async Task GetSnapshots(List<string> symbols)
        {
            int delay = 0;

            foreach (string symbol in symbols)
            {
                bool gotSnapshot = false;

                while (!gotSnapshot)
                {
                    Thread.Sleep(delay);

                    var response = await Http.GetAsync(B_Uris.Snapshot + symbol);
                    int statusCode = (int)response.StatusCode;

                    if (statusCode == 418 || statusCode == 429)
                    {
                        string delayHeader = response.Headers.GetValues("Retry-After").First();
                        delay = int.Parse(delayHeader) * 1000 + 500;
                        continue;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var snapshot = JsonConvert.DeserializeObject<B_Snapshot>(responseBody);
                    _snapshots[symbol] = snapshot;
                    gotSnapshot = true;

                    EmitSnapshot(snapshot, symbol);
                }
            }

            _fullyConnected = true;
        }

        public override async void Connect(List<string> symbols)
        {
            string feedNames = string.Join("@depth/", symbols).ToLower();
            _orderFeed = new WebSocketFeed<B_AggUpdate>(B_Uris.Websocket + feedNames);
            _orderFeedSubscription = _orderFeed.Subscribe(EmitAggregatedUpdate);

            _orderFeed.Connect();
            await GetSnapshots(symbols);            
            EmitBufferedUpdates(symbols);
        }

        private void EmitOrder(string symbol, OrderSide side, decimal[] orderTuple, long ms)
        {
            var time = DateTime.UnixEpoch.AddMilliseconds(ms / 1000);

            var order = new CurrencyOrder
            (
                exchange: this,
                symbol:   symbol,
                side:     side,
                price:    orderTuple[0],
                amount:   orderTuple[1],
                time:     time
            );

            lock (_observers)
            {
                _observers.ForEach(o => o.OnNext(order));
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