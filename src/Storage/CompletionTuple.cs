namespace CryptoBot.Storage
{
    public struct CompletionTuple
    {
        public double CompleteDays;
        public double TotalDays;

        public CompletionTuple(double completeDays, double totalDays)
        {
            CompleteDays = completeDays;
            TotalDays = totalDays;
        }
    }
}