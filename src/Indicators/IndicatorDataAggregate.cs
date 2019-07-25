using System;
using System.Collections.Generic;
using System.Linq;
using CryptoBot.Indicators.Renderers;

namespace CryptoBot.Indicators
{
    public class IndicatorDataAggregate
    {
        public readonly long TimeFrame;
        public readonly Dictionary<string, IndicatorData> Fields;
        public IndicatorData PrimaryField { get; private set; }

        public DateTime Start  => PrimaryField.Values.HeadTime;
        public DateTime End    => PrimaryField.Values.TailTime;
        public double   Min    => Fields.Values.Select(field => field.Min).Min();
        public double   Max    => Fields.Values.Select(field => field.Max).Max();
        public double   Domain => (double)(End - Start).TotalMilliseconds;
        public double   Range  => Max - Min;

        public IndicatorDataAggregate(long timeFrame)
        {
            Fields = new Dictionary<string, IndicatorData>();
            TimeFrame = timeFrame;
        }

        public void AddPrimaryField(string fieldName, IndicatorRenderer renderer)
        {
            if (PrimaryField != null)
                throw new Exception("Cannot define multiple primary fields");

            PrimaryField = new IndicatorData(fieldName, TimeFrame, renderer);
            Fields[fieldName] = PrimaryField;
        }

        public void AddField(string fieldName, IndicatorRenderer renderer) =>
            Fields[fieldName] = new IndicatorData(fieldName, TimeFrame, renderer);

        public void UpdateTail(string fieldName, object value) =>
            Fields[fieldName].UpdateTail(value);

        public void Record(string fieldName, object value, DateTime time) =>
            Fields[fieldName].Record(value, time);
    }
}