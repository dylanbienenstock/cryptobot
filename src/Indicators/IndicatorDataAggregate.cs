using System;
using System.Collections.Generic;
using System.Linq;
using CryptoBot.Indicators.Renderers;

namespace CryptoBot.Indicators
{
    public class IndicatorDataAggregate
    {
        public readonly int PeriodDuration;
        public readonly Dictionary<string, IndicatorData> Fields;
        public IndicatorData PrimaryField { get; private set; }

        public DateTime Start  => PrimaryField.Values.HeadTime;
        public DateTime End    => PrimaryField.Values.TailTime;
        public float    Min    => Fields.Values.Select(field => field.Min).Min();
        public float    Max    => Fields.Values.Select(field => field.Max).Max();
        public float    Domain => (float)(End - Start).TotalMilliseconds;
        public float    Range  => Max - Min;

        public IndicatorDataAggregate(int periodDuration)
        {
            Fields = new Dictionary<string, IndicatorData>();
            PeriodDuration = periodDuration;
        }

        public void AddPrimaryField(string fieldName, IndicatorRenderer renderer)
        {
            if (PrimaryField != null)
                throw new Exception("Cannot define multiple primary fields");

            PrimaryField = new IndicatorData(fieldName, PeriodDuration, renderer);
            Fields[fieldName] = PrimaryField;
        }

        public void AddField(string fieldName, IndicatorRenderer renderer) =>
            Fields[fieldName] = new IndicatorData(fieldName, PeriodDuration, renderer);

        public void Record(string fieldName, float value, DateTime time) =>
            Fields[fieldName].Record(value, time);
    }
}