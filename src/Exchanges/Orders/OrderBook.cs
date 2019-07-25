using System.Collections.Generic;
using CryptoBot.Exchanges;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.Exchanges.Orders
{
    public struct OrderBook
    {
        public OrderList Bids;
        public OrderList Asks;
        public readonly int Capacity;

        public decimal BestBid => Bids.Tail.Price;
        public decimal BestAsk => Asks.Tail.Price;

        public OrderBook(int capacity)
        {
            Bids = new OrderList(OrderSide.Bid, capacity);
            Asks = new OrderList(OrderSide.Ask, capacity);
            Capacity = capacity;
        }

        public void Record(CurrencyOrder order)
        {
            if (order.Side == OrderSide.Bid) Bids.Record(order);
            else                             Asks.Record(order);
        }
    }
}