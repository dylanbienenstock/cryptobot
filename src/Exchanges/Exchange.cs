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

        /// <summary>
        /// Cached result of <see cref="FetchSymbols"/> set by the
        ///  <see cref="ExchangeNetwork"/> that loads this adapter
        /// </summary>
        public List<string> Symbols;

        /// <summary>
        /// HTTP client to be shared between all adapters.
        /// TODO: Replace this with a shared instance of HttpBackoffClient
        /// </summary>
        public HttpClient Http;

        /// <summary>
        /// An observable that emits all orders received via websocket stream
        /// </summary>
        public abstract IObservable<CurrencyOrder> OrderStream { get; }

        /// <summary>
        /// An observable that emits all trades received via websocket stream
        /// </summary>
        public abstract IObservable<CurrencyTrade> TradeStream { get; }

        /// <summary>
        /// Contains static information about the exchange this adapter connects to
        /// </summary>
        public abstract ExchangeDetails Details { get; }

        /// <summary>
        /// Fetches a list of symbols (BTC/USDT, MATIC/BTC) that the exchange
        /// supports. Format is determined by the exchanges API.
        /// </summary>
        public abstract Task<List<string>> FetchSymbols();

        /// <summary>
        /// Fetches historical trading periods (OHLC + Vol) via HTTP request
        /// </summary>
        /// <param name="symbol">
        /// Symbol, in the exchange's format, to fetch data for
        /// </param>
        /// <param name="startTime">
        /// Time of the first (oldest) trading period to fetch
        /// </param>
        /// <param name="timeFrame">
        /// Granularity, 1 minute, 5 minutes, etc
        /// </param>
        /// <param name="count">
        /// Number of trading periods to fetch, going forward in time
        /// </param>
        /// <param name="priority">
        /// Priority of the HTTP request
        /// </param>
        public abstract Task<List<HistoricalTradingPeriod>> FetchTradingPeriods
            (string symbol, double startTime, long timeFrame, int count, int priority = 1);

        /// <summary>
        /// Attempts to find the first trading period for the given pair.
        /// The time of the returned period will be the pairs listing date.
        /// </summary>
        public abstract Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod(CurrencyPair pair);

        /// <summary>
        /// Connects to the exchange's websocket streams for the given symbols
        /// </summary>
        public abstract void Connect(List<string> symbols);

        /// <summary>
        /// Splits a symbol in the exchanges format into a generic format
        /// TODO: Change return type to a named tuple
        /// </summary>
        /// <param name="symbol">Symbol to split, in the exchanges format</param>
        /// <returns>A 2-element array in format { Base, Quote }</returns>
        public abstract string[] SplitSymbol(string symbol);

        /// <summary>
        /// Gets ticker information for all pairs on the exchange
        /// </summary>
        /// <returns>
        /// An array of objects containing price change, last price,
        /// and trading volume over the last 24 hours
        /// </returns>
        public abstract Task<MarketTicker[]> GetMarketTickers();

        /// <summary>
        /// Fetches historical trading periods (OHLC + Vol) via HTTP request
        /// </summary>
        /// <param name="market">Market to fetch data for</param>
        /// <param name="startTime">Time of the first (oldest) trading period to fetch</param>
        /// <param name="endTime">Time of the last (newest) trading period to fetch</param>
        /// <param name="timeFrame">Granularity, 1 minute, 5 minutes, etc</param>
        public Task<List<HistoricalTradingPeriod>> FetchTradingPeriods
            (Market market, double startTime, double endTime, long timeFrame = 60000) =>
                FetchTradingPeriods(market.Symbol, startTime, timeFrame, (int)((endTime - startTime) / timeFrame));

        /// <summary>
        /// All base and quote currencies supported by the exchange
        /// </summary>
        public List<Currency> Currencies;

        /// <summary>
        /// All markets supported by the exchange, indexed by pair
        /// </summary>
        public Dictionary<CurrencyPair, Market> Markets;

        /// <summary>
        /// Gets a market by pair
        /// </summary>
        public Market GetMarket(CurrencyPair pair) => Markets[pair];

        /// <summary>
        /// Gets a market by symbol, in the exchange's format
        /// </summary>
        public Market GetMarket(string symbol) => Markets[new CurrencyPair(this, symbol)];

        /// <summary>
        /// The exchange's name
        /// </summary>
        public string Name => Details.Name;

        /// <summary>
        /// The standard fee for taker transactions
        /// TODO: Replace this with a method
        /// </summary>
        public decimal Fee => Details.Fee;

        /// <summary>
        /// Converts a time frame, in milliseconds, into an
        /// interval name that represents it
        /// </summary>
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

        /// <summary>
        /// Converts an interval name into the time frame,
        /// in milliseconds, that it represents
        /// </summary>
        public static long GetTimeFrame(string intervalName)
        {
            switch (intervalName)
            {
                case "1m":  return 60000;
                case "3m":  return 180000;
                case "5m":  return 300000;
                case "15m": return 900000;
                case "30m": return 1800000;
                case "1h":  return 3600000;
                case "2h":  return 7200000;
                case "4h":  return 14400000;
                case "6h":  return 21600000;
                case "8h":  return 28800000;
                case "12h": return 43200000;
                case "1d":  return 86400000;
                case "3d":  return 259200000;
                case "1w":  return 604800000;
                case "1M":  return 2592000000;
                default: throw new Exception("Unsupported interval name: " + intervalName);
            }
        }

        /// <summary>
        /// Adds a market to the exchange:
        /// * Creates the Markets dictionary if it doesn't exist
        /// * Inserts it into the Markets dictionary
        /// * Creates the Currencies list if it doesn't exist
        /// * Inserts the base and quote currency into the Currencies list
        /// </summary>
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