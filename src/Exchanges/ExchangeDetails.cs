namespace CryptoBot.Exchanges
{
    /// <summary>
    /// Represents static information about an exchange
    /// </summary>
    public struct ExchangeDetails
    {
        /// <summary>
        /// The exchange's name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The exchange's standard taker fee
        /// </summary>
        public readonly decimal Fee;

        public ExchangeDetails(string name, decimal fee)
        {
            Name = name;
            Fee = fee;
        }
    }
}