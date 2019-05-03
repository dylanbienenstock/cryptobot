using System.Collections.Generic;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Orders;
using CryptoBot.Exchanges.Currencies;
using System;

namespace CryptoBot.Strategies
{
    public class Wallet
    {
        private Dictionary<Currency, decimal> _balances;
        private Exchange _exchange;

        public Wallet(Exchange exchange)
        {
            _balances = new Dictionary<Currency, decimal>();
            _exchange = exchange;

            foreach (var currency in exchange.Currencies)
                _balances[currency] = 0;
        }

        public decimal GetBalance(Currency currency) => _balances[currency];
        public void SetBalance(Currency currency, decimal balance) => _balances[currency] = balance;

        public void PlaceOrder(Transaction transaction)
        {
            var baseCurrency  = transaction.Market.Pair.Base;
            var quoteCurrency = transaction.Market.Pair.Quote;
            var sendTotal     = transaction.Price * transaction.Amount;
            var receiveTotal  = transaction.Price / transaction.Amount * (1 - _exchange.Fee);

            string baseCurrencyName  = Enum.GetName(typeof(Currency), baseCurrency);
            string quoteCurrencyName = Enum.GetName(typeof(Currency), quoteCurrency);

            if (transaction.Side == OrderSide.Bid)
            {
                if (_balances[quoteCurrency] < sendTotal)
                    throw new Exception("Insufficient funds");

                _balances[baseCurrency]  += receiveTotal;
                _balances[quoteCurrency] -= sendTotal;

                Journal.Log("BUY {receiveTotal} {baseCurrencyName} FOR {sendTotal} {quoteCurrencyName}");
            }
            else
            {
                if (_balances[baseCurrency] < sendTotal)
                    throw new Exception("Insufficient funds");

                _balances[baseCurrency]  -= sendTotal;
                _balances[quoteCurrency] += receiveTotal;

                Journal.Log("SELL {receiveTotal} {quoteCurrencyName} FOR {sendTotal} {baseCurrencyName}");
            }
        }
    }
}