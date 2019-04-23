using System;
using System.Collections;
using System.Collections.Generic;

namespace CryptoBot.Exchanges.Series
{
    public class StatisticalSeries<T> : IEnumerable<StatisticalSeriesNode<T>>
    {
        public StatisticalSeriesNode<T> Head;
        public StatisticalSeriesNode<T> Tail;
        public List<StatisticalSeriesReader<T>> Readers;
        public bool Complete;
        public int Count;
        private object _lockObj;

        public StatisticalSeries()
        {
            Readers = new List<StatisticalSeriesReader<T>>();
            Complete = false;
            Count = 0;
            _lockObj = new object();
        }

        public virtual void Record(T value) { }

        public void BindReader(StatisticalSeriesReader<T> reader) => Readers.Add(reader);

        protected void AppendTail(StatisticalSeriesNode<T> node)
        {
            lock (_lockObj)
            {
                if (Head == null)
                {
                    Head = node;
                    Tail = node;
                    Count = 1;
                    EmitPostAdd(node);
                    return;
                }

                node.Previous = Tail;
                Tail.Next = node;
                Tail = node;
                Count++;

                EmitPostAdd(node);
            }
        }

        public void UpdateTail(T value)
        {
            lock (_lockObj)
            {
                EmitPreUpdate(Tail);

                Tail.Value = value;
                
                EmitPostUpdate(Tail);
                EmitFinishModify(Tail);
            }
        }

        protected void DetachHead()
        {
            lock (_lockObj)
            {
                //if (Head == null) return;

                var node = Head;
                Head = Head.Next;

                if (Head != null)
                    Head.Previous = null;

                Count--;
                EmitPostRemove(node);
            }
        }

        private void EmitPostAdd(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostAdd(node));
        private void EmitPreUpdate(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPreUpdate(node));
        private void EmitPostUpdate(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostUpdate(node));
        private void EmitPostRemove(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostRemove(node));
        protected void EmitComplete() => Readers.ForEach(r => r.OnComplete());
        protected void EmitFinishModify(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnFinalizeRecord(node));

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<StatisticalSeriesNode<T>> GetEnumerator()
        {
            var node = Head;
            while (node != null)
            {
                yield return node;
                node = (StatisticalSeriesNode<T>)node.Next;
            }
        }
    }
}