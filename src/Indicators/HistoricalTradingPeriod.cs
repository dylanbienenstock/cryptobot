using System;

namespace CryptoBot.Indicators
{
    public class HistoricalTradingPeriod : TradingPeriod
    {
        public override bool Historical => true;

        public HistoricalTradingPeriod
        (
            DateTime time,
            decimal  open,
            decimal  high,
            decimal  low,
            decimal  close,
            decimal  volume
        ) : base(time, open, high, low, close, volume) { }

        public HistoricalTradingPeriod
        (
            decimal time,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            decimal volume
        ) : base(time, open, high, low, close, volume) { }

        public HistoricalTradingPeriod(decimal[] buckets) : base(buckets) { }
    }
}