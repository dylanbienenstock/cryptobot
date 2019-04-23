namespace CryptoBot.Exchanges.Currencies
{
    public class CurrencyVertex
    {
        public Exchange Exchange;
        public Currency Currency;
        public bool Visited;
        public double MinDistance;
        public CurrencyVertex Previous;

        public override int GetHashCode() => (Exchange.Index << 2) ^ (int)Currency;
    }
}