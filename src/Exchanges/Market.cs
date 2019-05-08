using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators;

namespace CryptoBot.Exchanges
{
    public class Market
    {
        public static TimeSpan TradeHistoryDuration = new TimeSpan(0, 0, 30);

        public Exchange Exchange;
        public string Symbol;
        public CurrencyPair Pair;
        public TimeSeries<CurrencyTrade> Trades;
        public OrderBook Orders;
        public bool UpToDate;
        
        public decimal BestBid => Orders.BestBid;
        public decimal BestAsk => Orders.BestAsk;

        public Market(Exchange exchange, string symbol)
        {
            Exchange = exchange;
            Symbol = symbol;
            Pair = new CurrencyPair(exchange, symbol);
            Trades = new TimeSeries<CurrencyTrade>(TradeHistoryDuration);
            Orders = new OrderBook(64);
            UpToDate = false;
        }

        public void RecordTrade(CurrencyTrade trade) => Trades.Record(trade);
        public void RecordOrder(CurrencyOrder order) => Orders.Record(order);

        public Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod() =>
            Exchange.GetFirstHistoricalTradingPeriod(Pair);
    }
}