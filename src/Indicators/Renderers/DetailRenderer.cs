using System;
using System.Drawing;
using CryptoBot.Exchanges;

namespace CryptoBot.Indicators.Renderers
{
    public class DetailRenderer : IndicatorRenderer
    {
        public DetailRenderer() : base(int.MaxValue) { }

        public override void Render()
        {
            using (var graphics = Graphics.FromImage(Context.Result))
            {
                Font font = new Font("Iosevka", 10);
                string interval = Exchange.GetIntervalName(Context.DataAggregate.TimeFrame);
                string title = $"{Context.Pair} [{interval}] {Context.Indicator.Name}";
                string subTitle = $"{DateTime.Now.ToString()}";
                int textX = (int)(Context.OuterBoundsMax * 0.05);
                int textY = (int)(Context.OuterBoundsMax * 0.02);
                var titleSize = graphics.MeasureString(title, font);
                var subTitleSize = graphics.MeasureString(subTitle, font);
                var titlePosition = new Point(textX, textY);
                var subTitlePosition = new Point(textX, textY + (int)(titleSize.Height * 1.3));
                var backdropColor = Color.FromArgb(150, Context.Background);
                var backdropPadding = 2;
                var titleBackdropRect = new Rectangle
                (
                    textX + backdropPadding / 2,
                    textY + backdropPadding / 2,
                    (int)Math.Max(titleSize.Width, subTitleSize.Width),
                    (int)(subTitlePosition.Y + subTitleSize.Height - textY)
                );
                titleBackdropRect.Inflate(backdropPadding, backdropPadding);

                graphics.FillRectangle(new SolidBrush(backdropColor), titleBackdropRect);
                graphics.DrawString(title, font, Brushes.WhiteSmoke, titlePosition);
                graphics.DrawString(subTitle, font, Brushes.Gray, subTitlePosition);

                string signal = Indicator.SignalToString(Context.Signal);
                var signalColor = Indicator.SignalToColor(Context.Signal);
                var signalSize = graphics.MeasureString(signal, font);
                int signalX = (int)(Context.Bounds.Right - Context.OuterBoundsMax * 0.05 - signalSize.Width);
                var signalPosition = new Point(signalX, textY);
                var signalBackdropRect = new Rectangle
                (
                    signalX + backdropPadding / 2,
                    textY + backdropPadding / 2,
                    (int)signalSize.Width,
                    (int)signalSize.Height
                );
                signalBackdropRect.Inflate(backdropPadding, backdropPadding);

                graphics.FillRectangle(new SolidBrush(backdropColor), signalBackdropRect);
                graphics.DrawString(signal, font, new SolidBrush(signalColor), signalPosition);
            }
        }
    }
}