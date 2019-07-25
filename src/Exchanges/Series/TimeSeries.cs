using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CryptoBot.Exchanges.Series
{
    public class TimeSeries<T> : StatisticalSeries<T>
    {
        public TimeSpan Duration;
        public Dictionary<StatisticalSeriesNode<T>, DateTime> _times;
        private object _lockObj;

        public DateTime HeadTime => GetTime(Head);
        public DateTime TailTime => GetTime(Tail);

        public TimeSeries(TimeSpan duration)
        {
            Duration = duration;
            _times = new Dictionary<StatisticalSeriesNode<T>, DateTime>();
            _lockObj = new object();
        }

        public override void Record(T value)
        {
            lock (_lockObj)
            {
                var time = (DateTime)((dynamic)value).Time;

                if (Tail != null && time < _times[Tail]) return;

                var node = new StatisticalSeriesNode<T>(value);
                _times[node] = time;

                AppendTail(node);
                RemoveExpiredRecords();
                EmitFinalizeRecord(node);
            }
        }

        public void Record(T value, DateTime time)
        {
            lock (_lockObj)
            {
                if (Tail != null && time < _times[Tail]) return;

                var node = new StatisticalSeriesNode<T>(value);
                _times[node] = time;

                AppendTail(node);
                RemoveExpiredRecords();
                EmitFinalizeRecord(node);
            }
        }

        private void RemoveExpiredRecords()
        {
            lock (_lockObj)
            {
                var removeNodes = new List<StatisticalSeriesNode<T>>();

                while (_times[Head] < (_times[Tail] - Duration))
                {
                    Complete = true;
                    removeNodes.Add(Head);
                    DetachHead();
                }

                // removeNodes.ForEach(n => _times.Remove(n));

                if (Complete) EmitComplete();
            }
        }

        public DateTime GetTime(StatisticalSeriesNode<T> node) => _times[node];
    }
}