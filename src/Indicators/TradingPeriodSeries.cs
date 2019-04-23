using System;
using System.Collections.Generic;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;

namespace CryptoBot.Indicators
{
    public class TradingPeriodSeries : StatisticalSeriesReader<CurrencyTrade>
    {
        public CapacitySeries<TradingPeriod> Values;
        public readonly int Periods;
        private double _periodDurationMilliseconds;
        private DateTime _currentPeriodStart;
        private DateTime _nextPeriodStart;

        public TradingPeriodSeries(TimeSeries<CurrencyTrade> timeseries, int periodDurationMilliseconds, int periods) : base(timeseries)
        {
            Values = new CapacitySeries<TradingPeriod>(periods);
            Periods = periods;
            _periodDurationMilliseconds = periodDurationMilliseconds;
        }

        public override void OnPostAdd(StatisticalSeriesNode<CurrencyTrade> node)
        {
            var quantizedMilliseconds = node.Value.GetQuantizedMilliseconds(_periodDurationMilliseconds);
            var quantizedTime = (DateTime.UnixEpoch).AddMilliseconds(quantizedMilliseconds);

            if (Values.Head == null || AfterCurrentPeriod(quantizedTime))
                CreateNewPeriod(node, quantizedMilliseconds, quantizedTime);
            else if (WithinCurrentPeriod(quantizedTime))
                UpdateCurrentPeriod(node, quantizedTime);
        }

        public override void OnPostRemove(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnPreUpdate(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnPostUpdate(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnComplete() { }
        public override void OnFinalizeRecord(StatisticalSeriesNode<CurrencyTrade> node) { }

        public void Add(TradingPeriod period)
        {
            var nodeMilliseconds = (period.Time - DateTime.UnixEpoch).TotalMilliseconds;
            var quantizedMilliseconds =
                Math.Floor(nodeMilliseconds / _periodDurationMilliseconds)
                * _periodDurationMilliseconds;
            var quantizedTime = (DateTime.UnixEpoch).AddMilliseconds(quantizedMilliseconds);

            FillGaps(quantizedMilliseconds, true);
            Values.Record(period);
        }

        private void FillGaps(double nextPeriodMilliseconds, bool historic = false)
        {
            if (Values.Tail != null)
            {
                var previousPeriodEndMilliseconds = 
                    (Values.Tail.Value.Time - DateTime.UnixEpoch).TotalMilliseconds
                    + _periodDurationMilliseconds;

                var gaps = (previousPeriodEndMilliseconds - nextPeriodMilliseconds)
                    / _periodDurationMilliseconds;

                for (int i = 0; i < gaps; i++)
                {   
                    var fillPeriodTime = Values.Tail.Value.Time
                        .AddMilliseconds(_periodDurationMilliseconds);

                    Values.Record(new TradingPeriod
                    {
                        Time  = fillPeriodTime,
                        Open  = Values.Tail.Value.Open,
                        High  = Values.Tail.Value.High,
                        Low   = Values.Tail.Value.Low,
                        Close = Values.Tail.Value.Close,
                    });
                }
            }
        }

        private void CreateNewPeriod(StatisticalSeriesNode<CurrencyTrade> node, double quantizedMilliseconds, DateTime quantizedTime)
        {
            FillGaps(quantizedMilliseconds);

            Values.Record(new TradingPeriod
            {
                Time  = quantizedTime,
                Open  = node.Value.Price,
                High  = node.Value.Price,
                Low   = node.Value.Price,
                Close = node.Value.Price,
            });

            _currentPeriodStart = quantizedTime;
            _nextPeriodStart = _currentPeriodStart.AddMilliseconds(_periodDurationMilliseconds);
        }

        private void UpdateCurrentPeriod(StatisticalSeriesNode<CurrencyTrade> node, DateTime quantizedTime)
        {
            Values.UpdateTail(new TradingPeriod
            {
                Time  = quantizedTime,
                Open  = Values.Tail.Value.Open,
                High  = Math.Max(Values.Tail.Value.High, node.Value.Price),
                Low   = Math.Min(Values.Tail.Value.Low, node.Value.Price),
                Close = node.Value.Price,
            });
        }

        private bool WithinCurrentPeriod(DateTime quantizedTime) => 
            quantizedTime >= _currentPeriodStart && quantizedTime < _nextPeriodStart;

        private bool AfterCurrentPeriod(DateTime quantizedTime) => 
            quantizedTime >= _nextPeriodStart;
    }
}