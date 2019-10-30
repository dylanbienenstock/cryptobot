using System;

namespace CryptoBot.Indicators
{
    public class IndicatorLease : IDisposable
    {
        public Indicator Indicator;

        public IndicatorLease(Indicator indicator)
        {
            Indicator = indicator;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}