using System;
using System.Collections.Generic;
using System.Linq;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Indicators;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine]
    public class IndicatorMultiInstance<T> where T : Indicator
    {
        public int Length;
        private Dictionary<Exchange, Dictionary<CurrencyPair, T>> _indicators;

        public IndicatorMultiInstance()
        {
            _indicators = new Dictionary<Exchange, Dictionary<CurrencyPair, T>>();
            Length = 0;
        }

        public IndicatorMultiInstance(IEnumerable<dynamic> indicators)
        {
            _indicators = new Dictionary<Exchange, Dictionary<CurrencyPair, T>>();

            if (indicators == null)
            {
                Length = 0;
                return;
            }

            foreach (var indicator in indicators)
            {
                if (!_indicators.ContainsKey(indicator.Market.Exchange))
                    _indicators[indicator.Market.Exchange] = new Dictionary<CurrencyPair, T>();

                _indicators[indicator.Market.Exchange][indicator.Market.Pair] = (T)indicator;
            }

            Length = _indicators.Count;
        }

        public T[] ToArray()
        {
            return _indicators.Values.SelectMany(e => e.Values).ToArray();
        } 

        public void ForEach(Action<T, string, string> action)
        {
            ToArray().ToList()
                .ForEach(i => action.Invoke(i, i.Market.Exchange.Name, i.Market.Pair.ToGenericSymbol()));
        }

        public IndicatorMultiInstance<T> ForExchange(string exchangeName)
        {
            return new IndicatorMultiInstance<T>(ToArray().Where(i => i.Market.Exchange.Name == exchangeName));
        }

        public IndicatorMultiInstance<T> ForPair(string symbol)
        {
            return new IndicatorMultiInstance<T>(ToArray().Where(i => i.Market.Pair.ToGenericSymbol() == symbol));
        }

        public IndicatorMultiInstance<T> ForBaseCurrency(string baseCurrency)
        {
            return new IndicatorMultiInstance<T>(ToArray().Where(i => i.Market.Pair.BaseName == baseCurrency));
        }

        public IndicatorMultiInstance<T> ForQuoteCurrency(string quoteCurrency)
        {
            return new IndicatorMultiInstance<T>(ToArray().Where(i => i.Market.Pair.QuoteName == quoteCurrency));
        }
    }

    [TypescriptDefine("__dont")]
    public class IndicatorMultiInstanceCustomDefinitions
    {
        [TypescriptCustomDefinition]
        public static string DefineForEach()
        {
return @"declare interface IndicatorMultiInstance<T> {
    forEach(callbackFn: (indicator: T, pair?: string, exchange?: string) => void): void;
}";
        }
    }
}