namespace CryptoBot.Exchanges
{
    public struct ExchangeDetails
    {
        public readonly string Name;
        public readonly decimal Fee;

        public ExchangeDetails(string name, decimal fee)
        {
            Name = name;
            Fee = fee;
        }
    }
}