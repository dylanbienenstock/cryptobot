using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using CryptoBot.Exchanges.Series;

namespace CryptoBot.Indicators.Renderers
{
    public abstract class IndicatorRenderer
    {
        public readonly int           Order;
        public IndicatorRenderContext Context        { get; private set; }
        public IndicatorData          Data           { get; private set; }
        public int                    PeriodDuration { get; private set; }

        public DateTime Now => Data.Values.TailTime.Quantize(Data.PeriodDuration);

        private float _scaleX => Context.Bounds.Width / Context.DataAggregate.Domain;
        private float _min;
        private float _scaleY;

        public IndicatorRenderer(int order)
        {
            Order = order;
        }

        public virtual void Render() { }

        public void SetRange((float min, float max) range)
        {
            _min = range.min;
            _scaleY = Context.Bounds.Height / (range.max - range.min);
        }

        public virtual (float min, float max) GetRange() =>
            (Context.DataAggregate.Min, Context.DataAggregate.Max);

        public static void Render(IndicatorRenderContext context, string fileName = null)
        {
            using (var graphics = Graphics.FromImage(context.Result))
            {
                var brush = new SolidBrush(context.Background);
                graphics.FillRectangle(brush, context.OuterBounds);
            }

            var primaryRenderer = context.DataAggregate.PrimaryField.Renderer;
            primaryRenderer.Context = context;

            var range = primaryRenderer.GetRange();
            var fields = context.DataAggregate.Fields.Values
                .OrderBy(field => field.Renderer.Order);

            foreach (var field in fields)
            {
                field.Renderer.Context = context;
                field.Renderer.Data = field;
                field.Renderer.SetRange(range);
                field.Renderer.Render();
            }

            var detailRenderer = new DetailRenderer();
            detailRenderer.Context = context;
            detailRenderer.Render();
        }

        public DateTime GetTime(StatisticalSeriesNode<float> node) =>
            Data.Values.GetTime(node).Quantize(Data.PeriodDuration);

        public float GetMillisecondsAgo(DateTime time) => 
            (float)(Now - time).TotalMilliseconds;

        public float GetMillisecondsAgo(StatisticalSeriesNode<float> node) => 
            (float)(Now - GetTime(node)).TotalMilliseconds;

        public float MapX(float time)  => 
            Context.Bounds.X + Context.Bounds.Width - time * _scaleX;
            
        public float MapY(float value) => 
            Context.OuterBounds.Height - (Context.Bounds.Y + (value - _min) * _scaleY);
        
        public float MapX(StatisticalSeriesNode<float> node) => MapX(GetMillisecondsAgo(node));
        public float MapY(StatisticalSeriesNode<float> node) => MapY(node.Value);

        public PointF MapXY(float time, float value)           => new PointF(MapX(time), MapY(value));
        public PointF MapXY(StatisticalSeriesNode<float> node) => new PointF(MapX(node), MapY(node));
    }
}