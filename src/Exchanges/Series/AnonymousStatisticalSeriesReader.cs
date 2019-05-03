using System;

namespace CryptoBot.Exchanges.Series
{
    public class AnonymousStatisticalSeriesReader<T> : StatisticalSeriesReader<T>
    {
        public Action<StatisticalSeriesNode<T>> OnPreAddAction;
        public Action<StatisticalSeriesNode<T>> OnPostAddAction;
        public Action<StatisticalSeriesNode<T>> OnPreUpdateAction;
        public Action<StatisticalSeriesNode<T>> OnPostUpdateAction;
        public Action<StatisticalSeriesNode<T>> OnPostRemoveAction;
        public Action<StatisticalSeriesNode<T>> OnFinalizeRecordAction;
        public Action OnCompleteAction;
        
        public AnonymousStatisticalSeriesReader
        (
            Action<StatisticalSeriesNode<T>> onPreAdd = null,
            Action<StatisticalSeriesNode<T>> onPostAdd = null,
            Action<StatisticalSeriesNode<T>> onPreUpdate = null,
            Action<StatisticalSeriesNode<T>> onPostUpdate = null,
            Action<StatisticalSeriesNode<T>> onPostRemove = null,
            Action<StatisticalSeriesNode<T>> onFinalizeRecord = null,
            Action onComplete = null
        )
        {
            OnPreAddAction = onPreAdd;
            OnPostAddAction = onPostAdd;
            OnPreUpdateAction = onPreUpdate;
            OnPostUpdateAction = onPostUpdate;
            OnPostRemoveAction = onPostRemove;
            OnCompleteAction = onComplete;
            OnFinalizeRecordAction = onFinalizeRecord;
        }

        public override void OnPreAdd(StatisticalSeriesNode<T> node)
        {
            if (OnPreAddAction != null) OnPreAddAction(node);
        }
        
        public override void OnPostAdd(StatisticalSeriesNode<T> node)
        {
            if (OnPostAddAction != null) OnPostAddAction(node);
        }
        
        public override void OnPreUpdate(StatisticalSeriesNode<T> node)
        {
            if (OnPreUpdateAction != null) OnPreUpdateAction(node);
        }
        
        public override void OnPostUpdate(StatisticalSeriesNode<T> node)
        {
            if (OnPostUpdateAction != null) OnPostUpdateAction(node);
        }
        
        public override void OnPostRemove(StatisticalSeriesNode<T> node)
        {
            if (OnPostRemoveAction != null) OnPostRemoveAction(node);
        }
        
        public override void OnComplete()
        {
            if (OnCompleteAction != null) OnCompleteAction();
        }
        
        public override void OnFinalizeRecord(StatisticalSeriesNode<T> node)
        {
            if (OnFinalizeRecordAction != null) OnFinalizeRecordAction(node);
        }
    }
}