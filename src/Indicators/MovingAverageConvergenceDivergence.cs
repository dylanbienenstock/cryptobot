using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
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

    public class MovingAverageConvergenceDivergence : Indicator
    {
        private string _aspect;
        private MovingAverage _fast;
        private MovingAverage _slow;
        private MovingAverage _signal;

        private decimal _lastValue;
        public decimal Value { get; private set; }
        public bool Complete => Source.Complete;

        private decimal _macd => _fast.Average - _slow.Average;
        private decimal _macdHistogram => _macd - _signal.Average;

        public MovingAverageConvergenceDivergence() { }

        public override IndicatorDetails Details => new IndicatorDetails
        (
            name:       "Moving Average Convergence Divergence",
            oscillator: true,
            lagging:    true,
            type:       IndicatorType.Trend,
            settings:   new []
            {
                new IndicatorSetting
                (
                    key: "FastPeriods",
                    name: "Fast Periods",
                    type: IndicatorSettingType.Int,
                    defaultValue: 12
                ),
                new IndicatorSetting
                (
                    key: "SlowPeriods",
                    name: "Slow Periods",
                    type: IndicatorSettingType.Int,
                    defaultValue: 26
                ),
                new IndicatorSetting
                (
                    key: "SignalPeriods",
                    name: "Signal Periods",
                    type: IndicatorSettingType.Int,
                    defaultValue: 9
                ),
                new IndicatorSetting
                (
                    key: "Aspect",
                    name: "Source",
                    type: IndicatorSettingType.Aspect,
                    defaultValue: "Close"
                )
            }
        );

        public override void Configure(dynamic settings)
        {
            OutputField
            (
                name: "MACD",
                renderer: new LineRenderer
                (
                    order: 2,
                    color: IndicatorColor.Bearish
                )
            );

            OutputField
            (
                name: "Signal",
                renderer: new LineRenderer
                (
                    order: 1,
                    color: IndicatorColor.Bullish
                )
            );

            PrimaryOutputField
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

            var series = RequireInput(TimeFrame, settings.FastPeriods);
            BindTo(series.Values);

            _aspect = settings.Aspect;
            _fast   = new MovingAverage(Smoothing.Exponential, settings.FastPeriods);
            _slow   = new MovingAverage(Smoothing.Exponential, settings.SlowPeriods);
            _signal = new MovingAverage(Smoothing.Exponential, settings.SignalPeriods);
        }

        public override void OnFinalizeRecord(Node node)
        {
            if (!Source.Complete) return;

            _lastValue = Value;
            Value = _macdHistogram;
        }

        public override void OnTradingPeriodClose(Node node)
        {
            EmitNextValue(new Dictionary<string, object>()
            {
                { "MACD",      _macd           },
                { "Signal",    _signal.Average },
                { "Histogram", Value           }
            });

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

            EmitNextValue(new Dictionary<string, object>()
            {
                { "MACD",      _macd           },
                { "Signal",    _signal.Average },
                { "Histogram", Value           }
            });
        }
    }
}