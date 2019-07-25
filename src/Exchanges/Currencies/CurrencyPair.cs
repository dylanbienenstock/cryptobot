using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CryptoBot.Exchanges.Currencies
{
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

        public static CurrencyPair FromGenericSymbol(string symbol)
        {
            string[] splitSymbol = symbol.Split('/');
            return new CurrencyPair(splitSymbol[0], splitSymbol[1]);
        }

        private static Currency ParseBase(string _base)
        {
            if (!Enum.TryParse(_base, true, out Currency parsed))
                throw new Exception($"Could not parse base currency \"{_base}\"");

            return parsed;
        }

        private static Currency ParseQuote(string quote)
        {
            if (!Enum.TryParse(quote, true, out Currency parsed))
                throw new Exception($"Could not parse quote currency \"{quote}\"");
            
            return parsed;
        }


        public string ToGenericSymbol()
        {
            return Enum.GetName(typeof(Currency), Base) + '/' +
                   Enum.GetName(typeof(Currency), Quote);
        }
        
        public override string ToString() => ToGenericSymbol();

        public string ToString(string delimiter)
        {
            return Enum.GetName(typeof(Currency), Base) + delimiter +
                   Enum.GetName(typeof(Currency), Quote);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !this.GetType().Equals(obj.GetType()))
                return false;

            var p = (CurrencyPair)obj;
            return p.Base == Base && p.Quote == Quote;
        }

        public override int GetHashCode() =>
            ((int)Base << 2) ^ (int)Quote;
    }
}