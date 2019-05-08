using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Indicators;

namespace CryptoBot.Storage
{
    public class PairHistory
    {
        [Key]
        public int             Id              { get; set; }
        public string          Symbol          { get; set; }
        public DateTime?       CursorTime      { get; set; }
        public DateTime?       ListingTime     { get; set; }
        public ExchangeHistory ExchangeHistory { get; set; }
        public List<HistoricalTradingPeriod> TradingPeriods  { get; set; }

        public PairHistory()
        {
            TradingPeriods = new List<HistoricalTradingPeriod>();
        }

        public PairHistory(CurrencyPair pair, ExchangeHistory exchangeHistory)
        {
            Symbol = pair.ToGenericSymbol();
            ExchangeHistory = exchangeHistory;
            TradingPeriods = new List<HistoricalTradingPeriod>();
        }
    }
}