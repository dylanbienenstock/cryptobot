using System;
using System.Linq;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;

namespace CryptoBot.Indicators
{
    public class IndicatorData
    {
        public readonly string FieldName;
        public readonly int PeriodDuration;
        public readonly IndicatorRenderer Renderer;
        public readonly TimeSeries<float> Values;
        public float Min { get; private set; }
        public float Max { get; private set; }
        
        public IndicatorData(string fieldName, int periodDuration, IndicatorRenderer renderer)
        {
            FieldName = fieldName;
            PeriodDuration = periodDuration;
            Renderer = renderer;
            Values = new TimeSeries<float>(new TimeSpan(0, 1, 0, 0, 0));

            Values.BindAnonymousReader
            (
                OnPostAdd:    node => OnPostAdd(node.Value),
                OnPostRemove: node => OnPostRemove(node.Value)
            );
        }

        public void Record(float value, DateTime time)
        {
            FillGaps(time);
            Values.Record(value, time);
        }

        private void FillGaps(DateTime time)
        {
            if (Values.Count == 0) return;

            var lastTime = Values.TailTime;

            if ((time - lastTime).TotalMilliseconds > PeriodDuration)
            {
                var gaps = (time - lastTime).TotalMilliseconds / PeriodDuration;

                for (int i = 0; i < gaps; i++)
                    Values.Record(Values.Tail.Value, lastTime.AddMilliseconds(PeriodDuration * i));                    
            }
        }

        private void OnPostAdd(float value)
        {
            Min = Math.Min(Min, value);
            Max = Math.Max(Max, value);
        }

        private void OnPostRemove(float value)
        {
            var values = Values.Select(node => node.Value);

            if (value == Min) Min = values.Min();
            if (value == Max) Max = values.Max();
        }
    }
}