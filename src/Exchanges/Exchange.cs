using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;
using System.Collections.Generic;
using CryptoBot.Indicators;

namespace CryptoBot.Exchanges
{
    /// <summary>
    /// Base class for remote exchange adapters
    /// </summary>
    public abstract class Exchange : IDisposable
    {
        public ExchangeNetwork Network;

        /// <summary>
        /// The order in which it was loaded by its containing <see cref="ExchangeNetwork"/>
        /// </summary>
        public int Index;
        public List<string> Symbols;
        public HttpClient Http;

        public abstract IObservable<CurrencyOrder> OrderStream { get; }
        public abstract IObservable<CurrencyTrade> TradeStream { get; }
        public abstract ExchangeDetails Details { get; }

        public abstract Task<List<string>> FetchSymbols();
        public abstract Task<List<HistoricalTradingPeriod>> FetchTradingPeriods
            (string symbol, double startTime, long timeFrame, int count, int priority = 1);
        public abstract Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod(CurrencyPair pair);
        public abstract void Connect(List<string> symbols);
        public abstract string[] SplitSymbol(string symbol);

        public abstract Task<MarketTicker[]> GetMarketTickers();

        public abstract decimal GetAmountStepSize(string symbol);

        public Task<List<HistoricalTradingPeriod>> FetchTradingPeriods
            (Market market, double startTime, double endTime, long timeFrame = 60000) =>
                FetchTradingPeriods(market.Symbol, startTime, timeFrame, (int)((endTime - startTime) / timeFrame));

        public List<Currency> Currencies;
        public Dictionary<CurrencyPair, Market> Markets;
        public Market GetMarket(CurrencyPair pair) => Markets[pair];
        public Market GetMarket(string symbol) => Markets[new CurrencyPair(this, symbol)];

        public string Name => Details.Name;
        public decimal Fee => Details.Fee;

        public static string GetIntervalName(long timeFrame)
        {
            switch (timeFrame)
            {
                case 60000:      return "1m";
                case 180000:     return "3m";
                case 300000:     return "5m";
                case 900000:     return "15m";
                case 1800000:    return "30m";
                case 3600000:    return "1h";
                case 7200000:    return "2h";
                case 14400000:   return "4h";
                case 21600000:   return "6h";
                case 28800000:   return "8h";
                case 43200000:   return "12h";
                case 86400000:   return "1d";
                case 259200000:  return "3d";
                case 604800000:  return "1w";
                case 2592000000: return "1M";
                default: throw new Exception("Unsupported timeframe: " + timeFrame);
            }
        }

        public void AddMarket(Market market)
        {
            if (Markets == null) Markets = new Dictionary<CurrencyPair, Market>();

            Markets[market.Pair] = market;
            
            if (Currencies == null) Currencies = new List<Currency>();
            if (!Currencies.Contains(market.Pair.Base))  Currencies.Add(market.Pair.Base);
            if (!Currencies.Contains(market.Pair.Quote)) Currencies.Add(market.Pair.Quote);
        }

        void IDisposable.Dispose() { }
    }
}