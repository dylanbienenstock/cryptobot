using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.Strategies
{
    public struct Transaction
    {
        public readonly Market Market;
        public readonly OrderSide Side;
        public readonly OrderType Type;
        public readonly decimal Price;
        public readonly decimal Amount;

        public Transaction
        (
            Market    market,
            OrderSide side,
            OrderType type,
            decimal   price,
            decimal   amount = 0
        ) {
            Market = market;
            Side   = side;
            Type   = type;
            Price  = price;
            Amount = amount;

            if (Type == OrderType.Market)
                Amount = Side == OrderSide.Bid
                    ? Market.BestAsk
                    : Market.BestBid;
        }
    }
}