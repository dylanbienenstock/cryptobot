using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;

namespace CryptoBot.Indicators.Renderers
{
    public class HistogramRenderer : IndicatorRenderer
    {
        private double _baseline;
        private double _barGap;
        private Color _aboveColor;
        private Color _belowColor;

        public HistogramRenderer
        (
            int   order,
            double baseline,
            Color above,
            Color below,
            double barGap = 1
        )
        : base(order)
        {
            _baseline      = baseline;
            _barGap        = barGap;
            _aboveColor    = above;
            _belowColor    = below;
        }

        public override void Render()
        {
            using (var graphics = Graphics.FromImage(Context.Result))
            {
                var aboveBrush = new SolidBrush(_aboveColor);
                var belowBrush = new SolidBrush(_belowColor);
                var aboveBars  = GetRectangleArray(val => val >= 0);
                var belowBars  = GetRectangleArray(val => val < 0);

                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.FillRectangles(aboveBrush, aboveBars);
                graphics.FillRectangles(belowBrush, belowBars);
            }
        }

        public override (double min, double max) GetRange()
        {
            var distanceFromBaseline = Math.Max
            (
                Math.Abs(_baseline - Context.DataAggregate.Min),
                Math.Abs(_baseline - Context.DataAggregate.Max)
            );

            var min = _baseline - distanceFromBaseline;
            var max = _baseline + distanceFromBaseline;

            return (min, max);
        }

        private RectangleF[] GetRectangleArray(Func<double, bool> selector) => 
            Data.Values.ToList()
                .Where (node => selector((double)node.Value))
                .Select(node => GetRectangle(node))
                .ToArray();

        private RectangleF GetRectangle(StatisticalSeriesNode<object> node)
        {
            double width     = (double)Context.Bounds.Width / Data.Values.Count - _barGap * 2;
            double barX      = MapX(node) + _barGap - width / 2;
            double barY      = MapY(node);
            double baselineY = MapY(_baseline);

            if ((double)node.Value >= 0)
                return new RectangleF((float)barX, (float)barY, (float)width, (float)baselineY - (float)barY);

            return new RectangleF((float)barX, (float)baselineY, (float)width, (float)barY - (float)baselineY);
        }
    }
}