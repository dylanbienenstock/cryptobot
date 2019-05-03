using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Series;

/*

MACD      = EMA(slow_periods, price) - EMA(fast_periods, price)
Signal    = EMA(signal_periods, MACD)
Histogram = MACD - Signal

A signal is emitted when the histogram crosses the base line (zero)
The signal is bullish if Histogram is positive
The signal is bearish if Histogram is negative

*/

namespace CryptoBot.Indicators
{
    using Node = StatisticalSeriesNode<TradingPeriod>;

    public static class MACDFactory
    {
        public static IndicatorMultiInstance<MovingAverageConvergenceDivergence> MACD
        (
            this IndicatorFactory factory,
            int periodDuration = IndicatorDefaults.PeriodDuration,
            int fastPeriods = 12,
            int slowPeriods = 26,
            int signalPeriods = 9,
            TradingPeriodAspect aspect = IndicatorDefaults.Aspect
        ) {
            return factory.CreateRaw<MovingAverageConvergenceDivergence>
            (
                periodDuration,
                fastPeriods,
                slowPeriods,
                signalPeriods,
                aspect
            );
        }
    }

    public class MovingAverageConvergenceDivergence : Indicator
    {
        private TradingPeriodAspect _aspect;
        private MovingAverage _fast;
        private MovingAverage _slow;
        private MovingAverage _signal;

        private decimal _lastValue;
        public decimal Value { get; private set; }
        public bool Complete => Source.Complete;

        private decimal _macd => _fast.Average - _slow.Average;
        private decimal _macdHistogram => _macd - _signal.Average;

        public MovingAverageConvergenceDivergence(IndicatorManifold manifold) : base("MACD", manifold) { }

        public override void Configure(params dynamic[] settings)
        {
            int periodDuration         = settings[0];
            int fastPeriods            = settings[1];
            int slowPeriods            = settings[2];
            int signalPeriods          = settings[3];
            TradingPeriodAspect aspect = settings[4];

            PeriodDuration(periodDuration);

            Output
            (
                name: "MACD",
                renderer: new LineRenderer
                (
                    order: 2,
                    color: IndicatorColor.Bearish
                )
            );

            Output
            (
                name: "Signal",
                renderer: new LineRenderer
                (
                    order: 1,
                    color: IndicatorColor.Bullish
                )
            );

            PrimaryOutput
            (
                name: "Histogram",
                renderer: new HistogramRenderer
                (
                    order: 0,
                    baseline: 0,
                    above: IndicatorColor.Bullish,
                    below: IndicatorColor.Bearish
                )
            );

            var series = Input(periodDuration, fastPeriods);
            BindTo(series.Values);

            _aspect = aspect;
            _fast   = new MovingAverage(Smoothing.Exponential, fastPeriods);
            _slow   = new MovingAverage(Smoothing.Exponential, slowPeriods);
            _signal = new MovingAverage(Smoothing.Exponential, signalPeriods);
        }

        public override void OnFinalizeRecord(Node node)
        {
            if (!Source.Complete) return;

            _lastValue = Value;
            Value = _macdHistogram;
        }

        public override void OnTradingPeriodClose(Node node)
        {
            EmitNextValue("MACD", _macd);
            EmitNextValue("Signal", _signal.Average);
            EmitNextValue("Histogram", Value);

            if (_lastValue <= 0 && Value > 0)
                EmitSignal(IndicatorSignal.Buy, $"Bullish Cross");
            else if (_lastValue >= 0 && Value < 0)
                EmitSignal(IndicatorSignal.Sell, $"Bearish Cross");
        }

        public override void OnPostAdd(Node node)
        {
            decimal price = node.Value.Get(_aspect);
            _fast.Add(price);
            _slow.Add(price);
            _signal.Add(_macd);
        }

        public override void OnPostRemove(Node node)
        {
            decimal price = node.Value.Get(_aspect);
            _fast.Subtract(price);
            _slow.Subtract(price);
            _signal.Subtract(_macd);
        }

        public override void OnPreUpdate(Node node)
        {
            decimal price = node.Value.Get(_aspect);
            _fast.SetUpdateValue(price);
            _slow.SetUpdateValue(price);
            _signal.SetUpdateValue(_macd);
        }

        public override void OnPostUpdate(Node node)
        {
            decimal price = node.Value.Get(_aspect);
            _fast.Update(price);
            _slow.Update(price);
            _signal.Update(_macd);
        }
    }
}