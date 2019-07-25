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
        public IndicatorRenderContext Context   { get; private set; }
        public IndicatorData          Data      { get; private set; }
        public long                   TimeFrame { get; private set; }

        public DateTime Now => Data.Values.TailTime.Quantize(Data.TimeFrame);

        private double _scaleX => Context.Bounds.Width / Context.DataAggregate.Domain;
        private double _min;
        private double _scaleY;

        public IndicatorRenderer(int order)
        {
            Order = order;
        }

        public virtual void Render() { }

        public void SetRange((double min, double max) range)
        {
            _min = range.min;
            _scaleY = Context.Bounds.Height / (range.max - range.min);
        }

        public virtual (double min, double max) GetRange() =>
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
                if (field.Renderer == null) continue;

                field.Renderer.Context = context;
                field.Renderer.Data = field;
                field.Renderer.SetRange(range);
                field.Renderer.Render();
            }

            var detailRenderer = new DetailRenderer();
            detailRenderer.Context = context;
            detailRenderer.Render();
        }

        public DateTime GetTime(StatisticalSeriesNode<object> node) =>
            Data.Values.GetTime(node).Quantize(Data.TimeFrame);

        public double GetMillisecondsAgo(DateTime time) => 
            (double)(Now - time).TotalMilliseconds;

        public double GetMillisecondsAgo(StatisticalSeriesNode<object> node) => 
            (double)(Now - GetTime(node)).TotalMilliseconds;

        public double MapX(object time)  => 
            Context.Bounds.X + Context.Bounds.Width - (double)time * _scaleX;
            
        public double MapY(object value) => 
            Context.OuterBounds.Height - (Context.Bounds.Y + ((double)value - _min) * _scaleY);
        
        public double MapX(StatisticalSeriesNode<object> node) => MapX(GetMillisecondsAgo(node));
        public double MapY(StatisticalSeriesNode<object> node) => MapY(node.Value);

        public PointF MapXY(object time, object value)           => new PointF((float)MapX(time), (float)MapY(value));
        public PointF MapXY(StatisticalSeriesNode<object> node) => new PointF((float)MapX(node), (float)MapY(node));
    }
}