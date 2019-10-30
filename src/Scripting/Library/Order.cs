using System;
using System.Dynamic;
using System.Globalization;
using CryptoBot.Scripting.Typings;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(defineConstructor: true)]
    public class Order
    {
        public OrderStatus Status;
        public OrderType Type;
        public OrderSide Side;
        public OrderTimeInEffect TimeInEffect;
        public double Price;

        [TypescriptOptionsType(typeof(OrderOptions))]
        public Order(dynamic options)
        {
            OrderOptions _options = ObjectBinder.BindOptions<OrderOptions>(options);

            Status = OrderStatus.Unplaced;
            Type = _options.Type;
            Side = _options.Side;
            TimeInEffect = _options.TimeInEffect;
            Price = _options.Price;
        }

        public enum OrderStatus
        {
            Unplaced,
            Placed,
            PartiallyFilled,
            Filled,
            Cancelled,
            Rejected,
            Expired
        }

        public enum OrderSide
        {
            Buy,
            Sell
        }

        public enum OrderType
        {
            Market,
            Limit,
            LimitMaker,
            StopLoss,
            StopLossLimit,
            TakeProfit,
            TakeProfitLimit,
        }

        public enum OrderTimeInEffect
        {
            GoodTilCancelled,
            ImmediateOrCancel,
            FillOrKill
        }

        public class OrderOptions
        {
            public OrderType Type;
            public OrderSide Side;
            public OrderTimeInEffect TimeInEffect;
            public double Price;

            public OrderOptions() {}
        }
    }
}