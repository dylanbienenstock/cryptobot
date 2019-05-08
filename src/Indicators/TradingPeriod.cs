using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CryptoBot.Indicators
{
    public class TradingPeriod
    {
        [Required]
        public DateTime Time { get; set; }

        [Required]
        public decimal Open { get; set; }

        [Required]
        public decimal High { get; set; }
                
        [Required]
        public decimal Low { get; set; }
                
        [Required]
        public decimal Close { get; set; }
                
        [Required]
        public decimal Volume { get; set; }

        [NotMapped]
        public bool Finished;

        [NotMapped]
        public virtual bool Historical => false;

        public TradingPeriod() { }

        public TradingPeriod
        (
            DateTime time,
            decimal  open,
            decimal  high,
            decimal  low,
            decimal  close,
            decimal  volume
        ) {
            Time   = time;
            Open   = open;
            High   = high;
            Low    = low;
            Close  = close;
            Volume = volume;
        }

        public TradingPeriod
        (
            decimal time,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            decimal volume
        ) {
            Time   = DateTimeExtension.FromMilliseconds((double)time);
            Open   = open;
            High   = high;
            Low    = low;
            Close  = close;
            Volume = volume;
        }

        public TradingPeriod(decimal[] buckets)
        {
            Time   = DateTimeExtension.FromMilliseconds((double)buckets[0]);
            Open   = buckets[1];
            High   = buckets[2];
            Low    = buckets[3];
            Close  = buckets[4];
            Volume = buckets[5];
        }

        public decimal Get(TradingPeriodAspect aspect)
        {
            switch (aspect)
            {
                case TradingPeriodAspect.Open:  return Open;
                case TradingPeriodAspect.High:  return High;
                case TradingPeriodAspect.Low:   return Low;
                case TradingPeriodAspect.Close: return Close;
            }

            throw new Exception();
        }

        public override string ToString() => 
            $"(O){Open} (H){High} (L){Low} (C){Close}";
    }
}