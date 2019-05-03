using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Series;

/*

Gains and losses are represented as positive percentages (0-100),
not absolute values ($...) or proper decimal fractions (0-1)

                    100
RSI = 100 - ───────────────────
            1 + MA₉ₐᵢₙ / MAₗₒₛₛ


"The [first] average gain and average loss are simple [n]-period averages
First Average Gain = Sum of Gains over the past [n] periods / [n].
First Average Loss = Sum of Losses over the past [n] periods / [n]"

 ── https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:relative_strength_index_rsi#calculation


"Note: It is important to remember that the Average Gain and Average Loss are
not true averages! Instead of dividing by the number of gaining [or losing] periods, 
total gains [or losses] are always divided by the specified number of time periods"

 ── http://cns.bu.edu/~gsc/CN710/fincast/Technical%20_indicators/Relative%20Strength%20Index%20(RSI).htm

*/

namespace CryptoBot.Indicators
{
    using Node = StatisticalSeriesNode<TradingPeriod>;

    public static class RSIFactory
    {
        public static IndicatorMultiInstance<RelativeStrengthIndex> RSI
        (
            this IndicatorFactory factory,
            int periodDuration = IndicatorDefaults.PeriodDuration,
            int periods = 14,
            TradingPeriodAspect aspect = IndicatorDefaults.Aspect
        ) {
            return factory.CreateRaw<RelativeStrengthIndex>
            (
                periodDuration,
                periods,
                aspect
            );
        }
    }

    public class RelativeStrengthIndex : Indicator
    {
        public decimal Value { get; private set; }
        public bool Complete => Source.Complete;
        private TradingPeriodAspect _aspect;
        private MovingAverage _gain;
        private MovingAverage _loss;
        private bool _empty;
        private int _periods;

        public RelativeStrengthIndex(IndicatorManifold manifold) : base("RSI", manifold) { }

        public override void Configure(params dynamic[] settings)
        {
            int periodDuration         = settings[0];
            int periods                = settings[1];
            TradingPeriodAspect aspect = settings[2];

            PeriodDuration(periodDuration);
            PrimaryOutput
            (
                name: "RSI",
                renderer: new ThresholdLineRenderer
                (
                    order:   0,
                    width:   3.0f,
                    levels:  new float[] { 0, 30, 70, 100 },
                    high:    IndicatorColor.Bearish,
                    neutral: IndicatorColor.Neutral,
                    low:     IndicatorColor.Bullish
                )
            );

            var series = Input(periodDuration, periods);
            BindTo(series.Values);

            _empty = true;
            _aspect = aspect;
            _gain = new MovingAverage(Smoothing.Modified, series.Periods);
            _loss = new MovingAverage(Smoothing.Modified, series.Periods);
        }
        
        public override void OnFinalizeRecord(Node node)
        {
            if (_empty) return;
            
            if (_loss.Average == 0)
            {
                Value = 100;
                return;
            }
            
            decimal relativeStrength = _gain.Average / _loss.Average;
            Value = 100.0m - 100.0m / (1.0m + relativeStrength);
        }

        public override void OnTradingPeriodClose(Node node)
        {
            EmitNextValue("RSI", Value);

            if      (Value > 80) EmitSignal(IndicatorSignal.StrongSell, $"Overbought");
            else if (Value > 70) EmitSignal(IndicatorSignal.Sell,       $"Overbought");
            else if (Value < 20) EmitSignal(IndicatorSignal.StrongBuy,  $"Oversold");
            else if (Value < 30) EmitSignal(IndicatorSignal.Buy,        $"Oversold");
            else                 EmitSignal(IndicatorSignal.Neutral,    $"Neutral");
        }
        
        public override void OnComplete()
        {
            decimal gainSum = 0;
            decimal lossSum = 0;
            
            foreach (var node in Source)
            {
                decimal change = node.GetChangePercentage(_aspect);
                if (change > 0) gainSum += change;
                else            lossSum -= change;
            }

            _periods = Source.Count;
            _gain.Add(gainSum);
            _loss.Add(lossSum);
            _empty = false;
        }

        public override void OnPostAdd(Node node)
        {
            if (_empty) return;
            
            decimal change = node.GetChangePercentage(_aspect);
            _gain.Add(change >= 0 ?  change : 0);
            _loss.Add(change <  0 ? -change : 0);
        }

        public override void OnPostRemove(Node node)
        {
            if (_empty) return;

            decimal change = node.GetChangePercentage(_aspect);
            _gain.Subtract(change >= 0 ?  change : 0);
            _loss.Subtract(change <  0 ? -change : 0);
        }

        public override void OnPreUpdate(Node node)
        {
            decimal change = node.GetChangePercentage(_aspect);
            _gain.SetUpdateValue(change >= 0 ?  change : 0);
            _loss.SetUpdateValue(change <  0 ? -change : 0);
        }

        public override void OnPostUpdate(Node node)
        {
            if (_empty) return;

            decimal change = node.GetChangePercentage(_aspect);
            _gain.Update(change >= 0 ?  change : 0);
            _loss.Update(change <  0 ? -change : 0);
        }
    }
}