using System;
using System.Linq;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;

namespace CryptoBot.Indicators
{
    public class IndicatorData
    {
        public readonly string FieldName;
        public readonly long TimeFrame;
        public readonly IndicatorRenderer Renderer;
        public readonly TimeSeries<object> Values;
        public double Min { get; private set; }
        public double Max { get; private set; }
        
        public IndicatorData(string fieldName, long timeFrame, IndicatorRenderer renderer)
        {
            FieldName = fieldName;
            TimeFrame = timeFrame;
            Renderer = renderer;
            Values = new TimeSeries<object>(new TimeSpan(0, 1, 0, 0, 0));

            Values.BindAnonymousReader
            (
                OnPostAdd:    node => OnPostAdd(node.Value),
                OnPostRemove: node => OnPostRemove(node.Value)
            );
        }

        public void Record(object value, DateTime time)
        {
            FillGaps(time);
            Values.Record(value, time);
        }

        public void UpdateTail(object value)
        {
            if (Values.Tail != null)
                Values.Tail.Value = value;
        }

        private void FillGaps(DateTime time)
        {
            if (Values.Count == 0) return;

            var lastTime = Values.TailTime;

            if ((time - lastTime).TotalMilliseconds > TimeFrame)
            {
                var gaps = (time - lastTime).TotalMilliseconds / TimeFrame;

                for (int i = 0; i < gaps; i++)
                    Values.Record(Values.Tail.Value, lastTime.AddMilliseconds(TimeFrame * i));                    
            }
        }

        private void OnPostAdd(object value)
        {
            // Min = Math.Min(Min, value);
            // Max = Math.Max(Max, value);
        }

        private void OnPostRemove(object value)
        {
            var values = Values.Select(node => node.Value);

            // if (value == Min) Min = values.Min();
            // if (value == Max) Max = values.Max();
        }
    }
}