using System;
using System.Collections.Generic;
using CryptoBot.Indicators;

namespace CryptoBot.Exchanges.Series
{
    public abstract class StatisticalSeriesReader<T>
    {
        public StatisticalSeries<T> Source;

        public void BindTo(StatisticalSeries<T> series)
        {
            Source = series;
            Source.BindReader(this);
        }

        public virtual void OnPreAdd(StatisticalSeriesNode<T> node) { }
        public virtual void OnPostAdd(StatisticalSeriesNode<T> node) { }
        public virtual void OnPreUpdate(StatisticalSeriesNode<T> node) { }
        public virtual void OnPostUpdate(StatisticalSeriesNode<T> node) { }
        public virtual void OnPostRemove(StatisticalSeriesNode<T> node) { }
        public virtual void OnComplete() { }
        public virtual void OnFinalizeRecord(StatisticalSeriesNode<T> node) { }
    }
}