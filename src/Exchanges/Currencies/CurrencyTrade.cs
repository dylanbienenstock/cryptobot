using System;
using System.Collections.Generic;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.Exchanges.Currencies
{
    public class CurrencyTrade
    {
        public Exchange     Exchange;
        public string       Symbol;
        public int          Id;
        public CurrencyPair Pair;
        public OrderSide    Side;
        public decimal      Price;
        public decimal      Amount;
        public DateTime     Time;

        public CurrencyTrade
        (
            Exchange  exchange,
            string    symbol,
            int       id,
            OrderSide side,
            decimal   price,
            decimal   amount,
            DateTime  time
        ) {
            Exchange = exchange;
            Symbol   = symbol;
            Id       = id;
            Pair     = new CurrencyPair(exchange, symbol);
            Side     = side;
            Price    = price;
            Amount   = amount;
            Time     = time;
        }

        public override string ToString()
        {
            return String.Format(
                "[{0}] {1} on {2} for {3} × {4}",
                Side == OrderSide.Bid ? "BUY" : "SELL",
                Symbol,
                Exchange.Details.Name,
                Price,
                Amount
            );
        }
    }
}