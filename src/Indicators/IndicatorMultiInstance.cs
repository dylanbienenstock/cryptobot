using System.Collections.Generic;
using CryptoBot.Exchanges;

namespace CryptoBot.Indicators
{
    public class IndicatorMultiInstance<T> where T : Indicator
    {
        private Dictionary<Market, T> _indicators;

        public IndicatorMultiInstance(Dictionary<Market, T> indicators)
        {
            _indicators = indicators;
        }

        public T GetInstanceFor(Market market)
        {
            return _indicators[market];
        }
    }
}