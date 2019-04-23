using System;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.Exchanges.Currencies
{
    public struct CurrencyEdge
    {
        public CurrencyVertex Home;
        public CurrencyVertex Away;
        public Market Market;
        public OrderSide Side;
        public double ExchangeRate;

        public double Weight => -Math.Log10(ExchangeRate);

        public override int GetHashCode() => (Home.GetHashCode() << 2) ^ Away.GetHashCode();
    }
}