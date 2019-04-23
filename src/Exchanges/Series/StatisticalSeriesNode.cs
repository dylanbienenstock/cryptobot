namespace CryptoBot.Exchanges.Series
{
    public class StatisticalSeriesNode<TValue>
    {
        public TValue Value;
        public StatisticalSeriesNode<TValue> Previous;
        public StatisticalSeriesNode<TValue> Next;
        public StatisticalSeriesNode(TValue value) => Value = value;
        public override string ToString() => Value.ToString();
    }
}