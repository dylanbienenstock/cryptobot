using CryptoBot.Exchanges.Series;

namespace CryptoBot.Indicators
{
    public abstract class TradingPeriodSeriesReader : StatisticalSeriesReader<TradingPeriod>
    {
        public override void OnPreAdd(StatisticalSeriesNode<TradingPeriod> node)
        {
            if (Source.Tail == null) return;

            Source.Tail.Value.Finished = true;
            OnTradingPeriodClose(Source.Tail);
        }

        public abstract void OnTradingPeriodClose(StatisticalSeriesNode<TradingPeriod> node);
    }
}