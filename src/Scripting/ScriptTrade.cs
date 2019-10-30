using System;

namespace CryptoBot.Scripting
{
    public class ScriptTrade
    {
        public DateTime PlacedTime;
        public TimeSpan TimeInEffect;

        public TimeSpan RemainingTimeInEffect =>
            DateTime.UtcNow - (PlacedTime + TimeInEffect);

        public enum OrderType
        {
            Market,
            Limit,
            StopLimit
        }

        public enum OrderSubType
        {
            GoodUntilTime,
            GoodUntilCancelled,
            FillOrKill,
            OneCancelsOthers
        }
    }
}