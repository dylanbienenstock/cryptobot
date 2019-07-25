namespace CryptoBot.Exchanges {
    public struct SyncStatusUpdate {
        public Exchange Exchange;
        public int Completed;
        public int Total;

        public SyncStatusUpdate(Exchange exchange, int completed, int total)
        {
            Exchange = exchange;
            Completed = completed;
            Total = total;
        }
    }
}