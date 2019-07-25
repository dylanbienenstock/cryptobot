namespace CryptoBot.Exchanges
{
    public struct MarketTicker
    {
        public Market Market;
        public string PriceChange;
        public string PriceChangePercentage;
        public string LastPrice;
        public string Volume;

        public MarketTicker
        (
            Market market,
            string priceChange,
            string priceChangePercentage,
            string lastPrice,
            string volume
        )
        {
            Market = market;
            PriceChange = priceChange;
            PriceChangePercentage = priceChangePercentage;
            LastPrice = lastPrice;
            Volume = volume;
        }
    }
}