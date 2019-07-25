namespace CryptoBot.Storage
{
    public struct CompletionTuple
    {
        public double CompleteDays;
        public double TotalDays;
        public bool   Collecting;

        public CompletionTuple
        (
            double completeDays,
            double totalDays,
            bool collecting
        )
        {
            CompleteDays = completeDays;
            TotalDays    = totalDays;
            Collecting   = collecting;
        }
    }
}