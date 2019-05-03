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
        private float _baseline;
        private float _barGap;
        private Color _aboveColor;
        private Color _belowColor;

        public HistogramRenderer
        (
            int   order,
            float baseline,
            Color above,
            Color below,
            float barGap = 1
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

        public override (float min, float max) GetRange()
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

        private RectangleF[] GetRectangleArray(Func<float, bool> selector) => 
            Data.Values.ToList()
                .Where (node => selector(node.Value))
                .Select(node => GetRectangle(node))
                .ToArray();

        private RectangleF GetRectangle(StatisticalSeriesNode<float> node)
        {
            float width     = (float)Context.Bounds.Width / Data.Values.Count - _barGap * 2;
            float barX      = MapX(node) + _barGap - width / 2;
            float barY      = MapY(node);
            float baselineY = MapY(_baseline);

            if (node.Value >= 0)
                return new RectangleF(barX, barY, width, baselineY - barY);

            return new RectangleF(barX, baselineY, width, barY - baselineY);
        }
    }
}