using System;

namespace CryptoBot.Indicators
{
    public struct TradingPeriod
    {
        public DateTime Time;
        public decimal Open;
        public decimal High;
        public decimal Low;
        public decimal Close;

        public enum Field
        {
            Open,
            High,
            Low,
            Close,
        }

        public decimal Get(Field fields)
        {
            switch (fields)
            {
                case Field.Open:  return Open;
                case Field.High:  return High;
                case Field.Low:   return Low;
                case Field.Close: return Close;
            }

            throw new Exception();
        }

        public override string ToString() => 
            $"(O){Open} (H){High} (L){Low} (C){Close}";
    }
}