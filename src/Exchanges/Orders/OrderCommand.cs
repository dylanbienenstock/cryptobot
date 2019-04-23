namespace CryptoBot.Exchanges.Orders
{
    public enum OrderCommandType
    {
        Market, Limit, Stop
    }

    public class OrderCommand
    {
        public readonly OrderCommandType Type;

        public OrderCommand(OrderCommandType type)
        {
            Type = type;
        }
    }

    public class MarketOrderCommand : OrderCommand
    {
        public Market Market;
        public OrderSide Side;
        public decimal Amount;

        public MarketOrderCommand(Market market, OrderSide side, decimal amount)
            : base(OrderCommandType.Market)
            {
                Market = market;
                Side = side;
                Amount = amount;
            }
    }

    public class LimitOrderCommand : OrderCommand
    {
        public Market Market;
        public OrderSide Side;
        public decimal Amount;
        public decimal LimitPrice;

        public LimitOrderCommand(Market market, OrderSide side, decimal amount, decimal limitPrice) 
            : base(OrderCommandType.Limit)
            {
                Market = market;
                Side = side;
                Amount = amount;
                LimitPrice = limitPrice;
            }
    }

    public class StopOrderCommand : OrderCommand
    {
        public Market Market;
        public OrderSide Side;
        public decimal Amount;
        public decimal LimitPrice;
        public decimal StopPrice;

        public StopOrderCommand(Market market, OrderSide side, decimal amount, decimal limitPrice, decimal stopPrice)
            : base(OrderCommandType.Stop)
            {
                Market = market;
                Side = side;
                Amount = amount;
                LimitPrice = limitPrice;
                StopPrice = stopPrice;
            }
    }
}