using System;

namespace CryptoBot.Series
{
    public enum Smoothing
    {
        Simple,
        Modified,
        Exponential
    }

    public class MovingAverage
    {
        public readonly Smoothing Type;
        public decimal Average { get; private set; }
        public decimal Slope { get; private set; }
        private int _periods;
        private int _count;
        private decimal _previousAverage;
        private decimal? _updateAverage;
        private decimal? _updateValue;
        private decimal _sum;
        private decimal _alpha;
        private bool _empty;

        public bool Complete => _count >= _periods;

        public MovingAverage(Smoothing type, int periods)
        {
            Type = type;
            _count = 0;
            _periods = periods;
            _previousAverage = 0;
            _sum = 0;
            _empty = true;

            switch (Type)
            {
                case Smoothing.Modified:
                    _alpha = 1.0m / _periods;
                    break;

                case Smoothing.Exponential:
                    _alpha = 2.0m / (_periods + 1.0m);
                    break;
            }
        }

        public void Add(decimal value)
        {
            _updateAverage = Average;
            _count++;

            switch (Type)
            {
                case Smoothing.Simple:
                    _sum += value;
                    Average = _sum / _count;
                    break;

                case Smoothing.Modified:
                case Smoothing.Exponential:
                    if (_empty) 
                        Average = value;
                    else 
                        Average = (value * _alpha) + (_previousAverage * (1.0m - _alpha));
                    break;
            }

            Slope = Average - _previousAverage;
            _empty = false;
            _previousAverage = Average;
        }

        public void Subtract(decimal value)
        {
            _count--;

            if (Type != Smoothing.Simple) return;
            
            _sum -= value;
            if (_count == 0) Average = _sum;
            else Average = _sum / _count;

            Slope = Average - _previousAverage;
            _previousAverage = Average;
        }

        public void SetUpdateValue(decimal value)
        {
            _updateValue = value;
        }

        public void Update(decimal value)
        {
            if (_empty || _updateAverage == null) return;

            switch (Type)
            {
                case Smoothing.Simple:
                    if (_updateValue == null) throw new Exception("No update value set");
                    Subtract((decimal)_updateValue);
                    Add(value);
                    break;

                case Smoothing.Modified:
                case Smoothing.Exponential:
                    Average = (value * _alpha) + ((decimal)_updateAverage * (1.0m - _alpha));
                    break;
            }
        }
    }
}