using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using CryptoBot.Exchanges;

namespace CryptoBot.Indicators.Renderers
{
    public class ThresholdLineRenderer : IndicatorRenderer
    {
        private double _min;
        private double _low;
        private double _high;
        private double _max;
        private double _width;
        private Color _highColor;
        private Color _neutralColor;
        private Color _lowColor;

        public ThresholdLineRenderer
        (
            int     order,
            double[] levels,
            Color   high,
            Color   neutral,
            Color   low,
            double   width = 1.5f
        )
        : base(order)
        {
            _min          = levels[0];
            _low          = levels[1];
            _high         = levels[2];
            _max          = levels[3];
            _highColor    = high;
            _neutralColor = neutral;
            _lowColor     = low;
            _width        = width;
        }

        public override (double min, double max) GetRange() => (_min, _max);

        public override void Render()
        {
            var points    = Data.Values
                .Select(node => MapXY(node))
                .ToArray();

            var w        = Context.OuterBounds.Width;
            var h        = Context.OuterBounds.Height;
            var highY    = (int)MapY(_high);
            var lowY     = (int)MapY(_low);
            var neutralH = lowY - highY;
            var lowH     = lowY;

            var colors = new Color[]
            {
                _neutralColor,
                _highColor,
                _lowColor
            };
            
            var clipRects = new Rectangle[]
            {
                new Rectangle(0, highY, w, neutralH), // Neutral
                new Rectangle(0,     0, w,    highY), // High
                new Rectangle(0,  lowY, w,     lowH)  // Low
            };

            using (var graphics = Graphics.FromImage(Context.Result))
            {
                var thresholdPen = new Pen(Color.FromArgb(64, _neutralColor), (float)_width);
                thresholdPen.DashStyle = DashStyle.Dash;

                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawLine(thresholdPen, 0, highY, w, highY);
                graphics.DrawLine(thresholdPen, 0,  lowY, w,  lowY);

                for (int i = 0; i < 3; i++)
                {
                    var subrender = new Bitmap(Context.OuterBounds.Width, Context.OuterBounds.Height);

                    using (var subgraphics = Graphics.FromImage(subrender))
                    {
                        subgraphics.SmoothingMode = SmoothingMode.HighQuality;
                        subgraphics.DrawLines(new Pen(colors[i], (float)_width), points);
                    }

                    graphics.DrawImage(subrender, clipRects[i], clipRects[i], GraphicsUnit.Pixel);
                }
            }
        }
    }
}