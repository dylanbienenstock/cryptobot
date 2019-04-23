using System;
using System.Collections.Generic;
using System.Linq;
using CryptoBot.Exchanges.Series;
using CryptoBot.Series;

/*
          ₙ  
SMA = 1÷n ∑ xᵢ 
         ᶦ⁼¹
 */

namespace CryptoBot.Indicators
{
    public class SimpleMovingAverage : StatisticalSeriesReader<decimal>
    {
        public decimal Value => _movingAverage.Average;
        public MovingAverage _movingAverage;

        public SimpleMovingAverage(StatisticalSeries<decimal> series) : base(series) => 
            _movingAverage = new MovingAverage(Smoothing.Simple, 0);

        public override void OnPostAdd(StatisticalSeriesNode<decimal> node) => 
            _movingAverage.Add(node.Value);

        public override void OnPreUpdate(StatisticalSeriesNode<decimal> node) => 
            _movingAverage.SetUpdateValue(node.Value);

        public override void OnPostUpdate(StatisticalSeriesNode<decimal> node) =>
            _movingAverage.Update(node.Value);

        public override void OnPostRemove(StatisticalSeriesNode<decimal> node) =>
            _movingAverage.Subtract(node.Value);

        public override void OnComplete() { }
        public override void OnFinalizeRecord(StatisticalSeriesNode<decimal> node) { }
    }
}