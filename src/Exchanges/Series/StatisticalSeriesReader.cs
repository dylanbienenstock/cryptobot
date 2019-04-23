using System;
using System.Collections.Generic;

namespace CryptoBot.Exchanges.Series
{
    public abstract class StatisticalSeriesReader<T>
    {
        public StatisticalSeries<T> Source;

        public StatisticalSeriesReader(StatisticalSeries<T> series)
        {
            Source = series;
            Source.BindReader(this);
        }

        public abstract void OnPostAdd(StatisticalSeriesNode<T> node);
        public abstract void OnPreUpdate(StatisticalSeriesNode<T> node);
        public abstract void OnPostUpdate(StatisticalSeriesNode<T> node);
        public abstract void OnPostRemove(StatisticalSeriesNode<T> node);
        public abstract void OnComplete();
        public abstract void OnFinalizeRecord(StatisticalSeriesNode<T> node);
    }
}