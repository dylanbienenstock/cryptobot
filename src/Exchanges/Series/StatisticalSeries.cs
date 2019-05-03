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

        public void BindAnonymousReader
        (
            Action<StatisticalSeriesNode<T>> OnPreAdd         = null,
            Action<StatisticalSeriesNode<T>> OnPostAdd        = null,
            Action<StatisticalSeriesNode<T>> OnPreUpdate      = null,
            Action<StatisticalSeriesNode<T>> OnPostUpdate     = null,
            Action<StatisticalSeriesNode<T>> OnPostRemove     = null,
            Action<StatisticalSeriesNode<T>> OnFinalizeRecord = null,
            Action                           OnComplete       = null
        )
        {
            var anonymousReader = new AnonymousStatisticalSeriesReader<T>
            (
                onPreAdd:         OnPreAdd,
                onPostAdd:        OnPostAdd,
                onPreUpdate:      OnPreUpdate,
                onPostUpdate:     OnPostUpdate,
                onPostRemove:     OnPostRemove,
                onFinalizeRecord: OnFinalizeRecord,
                onComplete:       OnComplete
            );

            BindReader(anonymousReader);
        }

        protected void AppendTail(StatisticalSeriesNode<T> node)
        {
            lock (_lockObj)
            {
                EmitPreAdd(node);

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
                EmitFinalizeRecord(Tail);
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

        private void EmitPreAdd(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPreAdd(node));
        private void EmitPostAdd(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostAdd(node));
        private void EmitPreUpdate(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPreUpdate(node));
        private void EmitPostUpdate(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostUpdate(node));
        private void EmitPostRemove(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnPostRemove(node));
        protected void EmitComplete() => Readers.ForEach(r => r.OnComplete());
        protected void EmitFinalizeRecord(StatisticalSeriesNode<T> node) => Readers.ForEach(r => r.OnFinalizeRecord(node));

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

        public List<StatisticalSeriesNode<T>> ToList()
        {
            var list = new List<StatisticalSeriesNode<T>>();

            foreach (var node in this)
                list.Add(node);

            return list;
        }
    }
}