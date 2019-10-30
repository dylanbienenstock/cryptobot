using System;
using System.Collections.Generic;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Series;

/*  
          ₙ    ┌ Hᵢ-Lᵢ,   ┐
ATR = 1÷n ∑ max||Hᵢ-Cᵢ₋₁|,|
         ᶦ⁼¹   └|Lᵢ-Cᵢ₋₁| ┘
*/

namespace CryptoBot.Indicators
{
    public class AverageTrueRange : StatisticalSeriesReader<TradingPeriod>
    {
        public decimal Value => _movingAverage.Average;
        public bool Complete => Source.Complete;
        private MovingAverage _movingAverage;

        public AverageTrueRange(Smoothing smoothing = Smoothing.Simple)
        {
            _movingAverage = new MovingAverage(smoothing, 14);
        }

        public override void OnPreAdd(StatisticalSeriesNode<TradingPeriod> node) { }

        public override void OnPostAdd(StatisticalSeriesNode<TradingPeriod> node) =>
            _movingAverage.Add(node.GetTrueRange());

        public override void OnPostRemove(StatisticalSeriesNode<TradingPeriod> node) => 
            _movingAverage.Subtract(node.GetTrueRange());

        public override void OnPreUpdate(StatisticalSeriesNode<TradingPeriod> node) => 
            _movingAverage.SetUpdateValue(node.GetTrueRange());

        public override void OnPostUpdate(StatisticalSeriesNode<TradingPeriod> node) =>
            _movingAverage.Update(node.GetTrueRange());

        public override void OnComplete() { }

        public override void OnFinalizeRecord(StatisticalSeriesNode<TradingPeriod> node)
        {
            Console.WriteLine("-------------------");
            Console.WriteLine(DateTime.Now.ToString());
            Console.WriteLine($"ATR{Source.Count}: {Value}");
        }
    }
}