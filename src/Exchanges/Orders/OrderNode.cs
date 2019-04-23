using System.Collections.Generic;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.Exchanges.Orders
{
    public class OrderNode
    {
        public decimal Price;
        public List<decimal> Amount;

        public OrderNode Previous;
        public OrderNode Next;

        public OrderNode(CurrencyOrder order)
        {
            Price = order.Price;
            Amount = new List<decimal>() { order.Amount };
        }
    }
}