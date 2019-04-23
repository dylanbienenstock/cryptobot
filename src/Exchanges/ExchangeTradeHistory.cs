using System;

namespace CryptoBot.Exchanges
{
    public struct ExchangeTradeHistory
    {
        public Action<Market> Apply;

        public ExchangeTradeHistory(Action<Market> apply)
        {
            Apply = apply;
        }
    }
}