using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CryptoBot.Exchanges.Series
{
    public class TimeSeries<T> : StatisticalSeries<T>
    {
        private TimeSpan _duration;
        private Dictionary<StatisticalSeriesNode<T>, DateTime> _times;
        private object _lockObj;

        public DateTime HeadTime => GetTime(Head);
        public DateTime TailTime => GetTime(Tail);

        public TimeSeries(TimeSpan duration)
        {
            _duration = duration;
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

        private void RemoveExpiredRecords()
        {
            lock (_lockObj)
            {
                var removeNodes = new List<StatisticalSeriesNode<T>>();

                while (_times[Head] < (_times[Tail] - _duration))
                {
                    Complete = true;
                    removeNodes.Add(Head);
                    DetachHead();
                }

                removeNodes.ForEach(n => _times.Remove(n));
                EmitComplete();
            }
        }
    }
}