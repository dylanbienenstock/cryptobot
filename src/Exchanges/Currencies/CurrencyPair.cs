using System;
using CryptoBot.TcpDebug.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CryptoBot.Exchanges.Currencies
{
    [JsonConverter(typeof(CurrencyPairConverter))]
    public class CurrencyPair
    {
        public Currency Base;
        public Currency Quote;

        public CurrencyPair(Currency _base, Currency quote)
        {
            Base = _base;
            Quote = quote;
        }

        public CurrencyPair(string _base, string quote)
        {
            Base = ParseBase(_base);
            Quote = ParseQuote(quote);
        }

        public CurrencyPair(Exchange exchange, string symbol)
        {
            string[] splitSymbol = exchange.SplitSymbol(symbol);

            Base = ParseBase(splitSymbol[0]);
            Quote = ParseQuote(splitSymbol[1]);
        }

        private static Currency ParseBase(string _base)
        {
            if (!Enum.TryParse(_base, true, out Currency parsed))
                throw new Exception("Could not parse base currency");

            return parsed;
        }

        private static Currency ParseQuote(string quote)
        {
            if (!Enum.TryParse(quote, true, out Currency parsed))
                throw new Exception("Could not parse quote currency");
            
            return parsed;
        }

        public string ToGenericSymbol()
        {
            return Enum.GetName(typeof(Currency), Base) + '/' +
                   Enum.GetName(typeof(Currency), Quote);
        }

        public string ToString(string delimiter)
        {
            return Enum.GetName(typeof(Currency), Base) + delimiter +
                   Enum.GetName(typeof(Currency), Quote);
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) 
            {
                return false;
            }

            var p = (CurrencyPair)obj;
            return p.Base == Base && p.Quote == Quote;
        }

        public override int GetHashCode()
        {
            return ((int)Base << 2) ^ (int)Quote;
        }
    }
}