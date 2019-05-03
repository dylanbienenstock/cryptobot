using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using CryptoBot.Exchanges;

namespace CryptoBot.Indicators.Renderers
{
    public class LineRenderer : IndicatorRenderer
    {
        private Color _color;
        private float _width;

        public LineRenderer
        (
            int order,
            Color color,
            float width = 1.5f
        )
        : base(order)
        {
            _color = color;
            _width = width;
        }

        public override void Render()
        {
            using (var graphics = Graphics.FromImage(Context.Result))
            {
                var points = Data.Values.Select(node => MapXY(node)).ToArray();

                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawLines(new Pen(Context.Background, _width * 2f), points);
                graphics.DrawLines(new Pen(_color, _width), points);
            }
        }
    }
}