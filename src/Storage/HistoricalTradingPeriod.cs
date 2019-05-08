using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.Indicators
{
    public class HistoricalTradingPeriod : TradingPeriod
    {
        [NotMapped]
        public override bool Historical => true;

        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Minute { get; set; }
        public int PairHistoryId { get; set; }

        public HistoricalTradingPeriod
        (
            DateTime time,
            decimal  open,
            decimal  high,
            decimal  low,
            decimal  close,
            decimal  volume
        )
        : base(time, open, high, low, close, volume)
        {
            Minute = (int)(time - DateTime.UnixEpoch).TotalMinutes;
        }

        public HistoricalTradingPeriod
        (
            decimal time,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            decimal volume
        )
        : base(time, open, high, low, close, volume)
        {
            Minute = (int)(time / 60000);
        }

        public HistoricalTradingPeriod(decimal[] buckets) : base(buckets)
        {
            Minute = (int)(buckets[0] / 60000);
        }
        
        public HistoricalTradingPeriod() : base() { }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;  
            if (this.GetType() != obj.GetType()) return false;  
    
            var period = (HistoricalTradingPeriod)obj;  

            return period.Minute == Minute;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                result = (result * 397) ^ Minute;
                result = (result * 397) ^ this.PairHistoryId;
                return result;
            }
        }
    }
}