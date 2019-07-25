using System;
using CryptoBot.Exchanges.Series;

namespace CryptoBot.Indicators
{
    public static class TradingPeriodMetrics
    {
        public static decimal GetChangePercentage(this StatisticalSeriesNode<TradingPeriod> node, string aspect)
        {
            if (node.Previous == null) return 0;
            decimal currentPrice = node.Value.Get(aspect);
            decimal previousPrice = node.Previous.Value.Get(aspect);
            return (currentPrice - previousPrice) / previousPrice;
        } 

        public static decimal GetTrueRange(this StatisticalSeriesNode<TradingPeriod> node)
        {
            var range1 = node.Value.High - node.Value.Low;
            if (node.Previous == null) return range1;
            var range2 = Math.Abs(node.Value.High - node.Previous.Value.Close);
            var range3 = Math.Abs(node.Value.Low - node.Previous.Value.Close);
            return Math.Max(range1, Math.Max(range2, range3));
        }
    }
}