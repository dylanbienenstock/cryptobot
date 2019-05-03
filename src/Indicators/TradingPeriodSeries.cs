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

        public TradingPeriodSeries(TimeSeries<CurrencyTrade> timeseries, int periodDurationMilliseconds, int periods)
        {
            Values = new CapacitySeries<TradingPeriod>(periods);
            Periods = periods;
            _periodDurationMilliseconds = periodDurationMilliseconds;

            BindTo(timeseries);
        }

        public override void OnPostAdd(StatisticalSeriesNode<CurrencyTrade> node)
        {
            var quantizedMilliseconds = node.Value.Time.GetQuantizedMilliseconds(_periodDurationMilliseconds);
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
            var quantizedMilliseconds = Math.Floor(nodeMilliseconds / _periodDurationMilliseconds)
                * _periodDurationMilliseconds;
            var quantizedTime = DateTime.UnixEpoch.AddMilliseconds(quantizedMilliseconds);

            if (Values.Tail != null)
            {
                var lastPeriod = Values.Tail.Value;

                if (quantizedTime == lastPeriod.Time)
                {
                    Values.UpdateTail
                    (
                        new TradingPeriod
                        (
                            time:   lastPeriod.Time,
                            open:   lastPeriod.Open,
                            high:   Math.Max(lastPeriod.High, period.High),
                            low:    Math.Min(lastPeriod.Low, period.Low),
                            close:  period.Close,
                            volume: lastPeriod.Volume + period.Volume
                        )
                    );

                    return;
                }
            }

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

                    var fillPeriod = new TradingPeriod
                    (
                        time:   fillPeriodTime,
                        open:   Values.Tail.Value.Open,
                        high:   Values.Tail.Value.High,
                        low:    Values.Tail.Value.Low,
                        close:  Values.Tail.Value.Close,
                        volume: Values.Tail.Value.Volume
                    );

                    fillPeriod.Finished = true;
                    Values.Record(fillPeriod);
                }
            }
        }

        public void OnPeriodClose()
        {
            if (Values.Tail == null) return;
            Values.Tail.Value.Finished = true;
        }

        private void CreateNewPeriod(StatisticalSeriesNode<CurrencyTrade> node, double quantizedMilliseconds, DateTime quantizedTime)
        {
            OnPeriodClose();
            FillGaps(quantizedMilliseconds);

            Values.Record(
                new TradingPeriod
                (
                    time:   quantizedTime,
                    open:   node.Value.Price,
                    high:   node.Value.Price,
                    low:    node.Value.Price,
                    close:  node.Value.Price,
                    volume: node.Value.Amount
                )
            );

            _currentPeriodStart = quantizedTime;
            _nextPeriodStart = _currentPeriodStart.AddMilliseconds(_periodDurationMilliseconds);
        }

        private void UpdateCurrentPeriod(StatisticalSeriesNode<CurrencyTrade> node, DateTime quantizedTime)
        {
            Values.UpdateTail(
                new TradingPeriod
                (
                    time:   quantizedTime,
                    open:   Values.Tail.Value.Open,
                    high:   Math.Max(Values.Tail.Value.High, node.Value.Price),
                    low:    Math.Min(Values.Tail.Value.Low, node.Value.Price),
                    close:  node.Value.Price,
                    volume: Values.Tail.Value.Volume + node.Value.Amount
                )
            );
        }

        private bool WithinCurrentPeriod(DateTime quantizedTime) => 
            quantizedTime >= _currentPeriodStart && quantizedTime < _nextPeriodStart;

        private bool AfterCurrentPeriod(DateTime quantizedTime) => 
            quantizedTime >= _nextPeriodStart;
    }
}