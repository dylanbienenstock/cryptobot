using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Series;
using CryptoBot.Scripting.Typings;

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

    public class RelativeStrengthIndex : Indicator
    {
        public decimal Value { get; private set; }
        public bool Complete => Source.Complete;
        private string _aspect;
        private MovingAverage _gain;
        private MovingAverage _loss;
        private bool _empty;
        private int _periods;

        public RelativeStrengthIndex() { }

        public override IndicatorDetails Details => new IndicatorDetails
        (
            name:       "Relative Strength Index",
            oscillator: true,
            lagging:    true,
            type:       IndicatorType.Momentum,
            settings:   new []
            {
                new IndicatorSetting
                (
                    key: "Periods",
                    name: "Periods",
                    type: IndicatorSettingType.Int,
                    defaultValue: 14
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
            PrimaryOutputField
            (
                name: "RSI",
                renderer: new ThresholdLineRenderer
                (
                    order:   0,
                    width:   3.0f,
                    levels:  new double[] { 0, 30, 70, 100 },
                    high:    IndicatorColor.Bearish,
                    neutral: IndicatorColor.Neutral,
                    low:     IndicatorColor.Bullish
                )
            );

            var series = RequireInput(TimeFrame, (int)settings.Periods);
            BindTo(series.Values);

            _empty = true;
            _aspect = (string)settings.Aspect;
            _gain = new MovingAverage(Smoothing.Modified, (int)settings.Periods);
            _loss = new MovingAverage(Smoothing.Modified, (int)settings.Periods);
        }
        
        public override void OnFinalizeRecord(Node node)
        {
            // Console.WriteLine(Source.Count);

            if (_empty) return;
            
            if (_loss.Average == 0)
            {
                Value = 100;
                return;
            }
            
            decimal relativeStrength = _gain.Average / _loss.Average;
            Value = 100.0m - 100.0m / (1.0m + relativeStrength);

            EmitNextValue(new Dictionary<string, object>()
            {
                { "RSI", Value }
            });
        }

        public override void OnTradingPeriodClose(Node node)
        {
            if      (Value > 80) EmitSignal(IndicatorSignal.StrongSell, $"Overbought");
            else if (Value > 70) EmitSignal(IndicatorSignal.Sell,       $"Overbought");
            else if (Value < 20) EmitSignal(IndicatorSignal.StrongBuy,  $"Oversold");
            else if (Value < 30) EmitSignal(IndicatorSignal.Buy,        $"Oversold");
            else                 EmitSignal(IndicatorSignal.Neutral,    $"Neutral");
        }
        
        [TypescriptIgnore]
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

            EmitNextValue(new Dictionary<string, object>()
            {
                { "RSI", Value }
            });
        }
    }
}