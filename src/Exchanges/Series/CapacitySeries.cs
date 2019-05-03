using System.Collections;
using System.Collections.Generic;

namespace CryptoBot.Exchanges.Series
{
    public class CapacitySeries<T> : StatisticalSeries<T>
    {
        public int Capacity { get; private set; }
        private object _lockObj;

        public CapacitySeries(int capacity)
        {
            Capacity = capacity;
            _lockObj = new object();
        }

        public override void Record(T value)
        {
            lock (_lockObj)
            {
                var node = new StatisticalSeriesNode<T>(value);

                AppendTail(node);

                if (Count == Capacity && !Complete)
                {
                    Complete = true;
                    EmitComplete();
                }

                if (Count > Capacity)
                    DetachHead();

                EmitFinalizeRecord(node);
            }
        }
    }
}