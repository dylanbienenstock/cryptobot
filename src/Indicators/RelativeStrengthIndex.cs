using System;
using System.Collections.Generic;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
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

    public class RelativeStrengthIndex : StatisticalSeriesReader<TradingPeriod>
    {
        public decimal Value { get; private set; }
        public bool Complete => Source.Complete;
        private TradingPeriod.Field _sourceField;
        private MovingAverage _gain;
        private MovingAverage _loss;
        private bool _empty;
        private int _periods;

        public RelativeStrengthIndex(TradingPeriod.Field sourceField, TradingPeriodSeries series) : base(series.Values)
        {
            _sourceField = sourceField;
            _gain = new MovingAverage(Smoothing.Modified, series.Periods);
            _loss = new MovingAverage(Smoothing.Modified, series.Periods);
            _empty = true;

            // * Writes CSV data to journal.log *
            // string lastLogTime = "";
            // new Thread(() =>
            // {
            //     while (true)
            //     {
            //         string time = DateTime.Now.Hour + "" + DateTime.Now.Minute;
            //         if (time != lastLogTime)
            //         {
            //             Journal.Log($"My Calculation,{time},{Value}", false);
            //             lastLogTime = time;
            //         }
            //         Thread.Sleep(5);
            //     }
            // }).Start();
            // * * * * * * * * * * * * * * * *
        }

        public override void OnPostAdd(Node node)    => AddChange(node);
        public override void OnPostRemove(Node node) => SubtractChange(node);
        public override void OnPreUpdate(Node node)  => SetUpdateValue(node);
        public override void OnPostUpdate(Node node) => UpdateChange(node);
        public override void OnComplete()            => AddInitialPeriod();
        
        public override void OnFinalizeRecord(Node node)
        {
            if (_empty) return;

            Recalculate();

            Console.WriteLine("---------------------------");
            Console.WriteLine($"AVG. GAIN: {_gain.Average}");
            Console.WriteLine($"AVG. LOSS: {_loss.Average}");
            Console.WriteLine($"CURR. TIME: {DateTime.Now}");
            Console.WriteLine($"RSI{Source.Count}: {Value}");
        }

        private void AddInitialPeriod()
        {
            decimal gainSum = 0;
            decimal lossSum = 0;
            
            foreach (var node in Source)
            {
                decimal change = node.GetChangePercentage(_sourceField);
                if (change > 0) gainSum += change;
                else            lossSum -= change;
            }

            _periods = Source.Count;
            _gain.Add(gainSum);
            _loss.Add(lossSum);
            _empty = false;
        }

        private void AddChange(Node node)
        {
            if (_empty) return;
            
            decimal change = node.GetChangePercentage(_sourceField);
            _gain.Add(change >= 0 ?  change : 0);
            _loss.Add(change <  0 ? -change : 0);
        }

        private void SubtractChange(Node node)
        {
            if (_empty) return;

            decimal change = node.GetChangePercentage(_sourceField);
            _gain.Subtract(change >= 0 ?  change : 0);
            _loss.Subtract(change <  0 ? -change : 0);
        }

        private void SetUpdateValue(Node node)
        {
            decimal change = node.GetChangePercentage(_sourceField);
            _gain.SetUpdateValue(change >= 0 ?  change : 0);
            _loss.SetUpdateValue(change <  0 ? -change : 0);
        }

        private void UpdateChange(Node node)
        {
            if (_empty) return;

            decimal change = node.GetChangePercentage(_sourceField);
            _gain.Update(change >= 0 ?  change : 0);
            _loss.Update(change <  0 ? -change : 0);
        }

        private void Recalculate()
        {
            if (_empty) return;

            if (_loss.Average == 0)
            {
                Value = 100;
                return;
            }
            
            decimal relativeStrength = _gain.Average / _loss.Average;
            Value = 100m - 100m / (1m + relativeStrength);
        }
    }
}