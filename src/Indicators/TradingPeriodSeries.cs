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
        private long _timeFrame;
        private DateTime _currentPeriodStart;
        private DateTime _nextPeriodStart;
        private object _lockObj;

        public TradingPeriodSeries(TimeSeries<CurrencyTrade> timeseries, long timeFrame, int periods)
        {
            Values = new CapacitySeries<TradingPeriod>(periods);
            Periods = periods;
            _timeFrame = timeFrame;
            _lockObj = new object();

            BindTo(timeseries);
        }

        public override void OnPostAdd(StatisticalSeriesNode<CurrencyTrade> node)
        {
            var quantizedMilliseconds = node.Value.Time.GetQuantizedMilliseconds(_timeFrame);
            var quantizedTime = (DateTime.UnixEpoch).AddMilliseconds(quantizedMilliseconds);

            lock (_lockObj)
            {
                if (Values.Head == null || AfterCurrentPeriod(quantizedTime))
                    CreateNewPeriod(node, quantizedMilliseconds, quantizedTime);
                else if (WithinCurrentPeriod(quantizedTime))
                    UpdateCurrentPeriod(node, quantizedTime);
            }
        }

        public override void OnPostRemove(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnPreUpdate(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnPostUpdate(StatisticalSeriesNode<CurrencyTrade> node) { }
        public override void OnComplete() { }
        public override void OnFinalizeRecord(StatisticalSeriesNode<CurrencyTrade> node) { }

        public void Add(TradingPeriod period)
        {
            var nodeMilliseconds = (period.Time - DateTime.UnixEpoch).TotalMilliseconds;
            var quantizedMillis = period.Time.GetQuantizedMilliseconds(_timeFrame);
            var quantizedTime = DateTime.UnixEpoch.AddMilliseconds(quantizedMillis);

            if (Values.Tail != null)
            {
                var lastPeriod = Values.Tail.Value;

                if (quantizedTime == lastPeriod.Time)
                {
                    Console.WriteLine("DUPLICATE!");

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

            FillGaps(quantizedMillis, true);
            Values.Record(period);
        }

        private void FillGaps(double nextPeriodMilliseconds, bool historic = false)
        {
            if (Values.Tail != null)
            {
                var previousPeriodEndMilliseconds = 
                    (Values.Tail.Value.Time - DateTime.UnixEpoch).TotalMilliseconds
                    + _timeFrame;

                var gaps = (previousPeriodEndMilliseconds - nextPeriodMilliseconds)
                    / _timeFrame;

                for (int i = 0; i < gaps; i++)
                {
                    var fillPeriodTime = Values.Tail.Value.Time
                        .AddMilliseconds(_timeFrame);

                    var fillPeriod = new TradingPeriod
                    (
                        time:   fillPeriodTime,
                        open:   Values.Tail.Value.Close,
                        high:   Values.Tail.Value.Close,
                        low:    Values.Tail.Value.Close,
                        close:  Values.Tail.Value.Close,
                        volume: 0
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

        private void CreateNewPeriod(StatisticalSeriesNode<CurrencyTrade> node, double quantizedMillis, DateTime quantizedTime)
        {
            OnPeriodClose();
            FillGaps(quantizedMillis);

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
            _nextPeriodStart = _currentPeriodStart.AddMilliseconds(_timeFrame);
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

        private bool WithinCurrentPeriod(DateTime time) => 
            time >= _currentPeriodStart && time < _nextPeriodStart;

        private bool AfterCurrentPeriod(DateTime time) => time >= _nextPeriodStart;
    }
}