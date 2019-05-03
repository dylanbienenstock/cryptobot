using System;
using System.Collections.Generic;
using System.Drawing;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;

namespace CryptoBot.Indicators
{
    public abstract class Indicator : TradingPeriodSeriesReader
    {
        public readonly string Name;
        public Market Market;
        public IndicatorManifold Manifold;
        public List<decimal> Settings;

        public Indicator(string name, IndicatorManifold manifold)
        {
            Name = name;
            Manifold = manifold;
        }

        public static string SignalToString(IndicatorSignal signal)
        { 
            switch (signal)
            {
                case IndicatorSignal.StrongSell:
                    return "⬊ Strong Sell";
                case IndicatorSignal.Sell:
                    return "⬊ Sell";
                case IndicatorSignal.Neutral:
                    return "Neutral";
                case IndicatorSignal.Buy:
                    return "⬈ Buy";
                case IndicatorSignal.StrongBuy:
                    return "⬈ Strong Buy";
            }

            return "Neutral";
        }

        public static Color SignalToColor(IndicatorSignal signal)
        { 
            switch (signal)
            {
                case IndicatorSignal.StrongSell:
                case IndicatorSignal.Sell:
                    return IndicatorColor.Bearish;
                case IndicatorSignal.Neutral:
                    return IndicatorColor.Neutral;
                case IndicatorSignal.Buy:
                case IndicatorSignal.StrongBuy:
                    return IndicatorColor.Bullish;
            }

            return IndicatorColor.Neutral;
        }
        
        public abstract void Configure(params dynamic[] settings);

        protected TradingPeriodSeries Input(int periodDurationMilliseconds, int periods) =>
            Manifold.RequireInput(this, periodDurationMilliseconds, periods);

        protected void PeriodDuration(int milliseconds) =>
            Manifold.RegisterPeriodDuration(this, milliseconds);

        protected void PeriodDuration(decimal milliseconds) =>
            PeriodDuration((int)milliseconds);

        protected void PrimaryOutput(string name, IndicatorRenderer renderer) =>
                Manifold.RegisterPrimaryOutput(this, name, renderer);

        protected void Output(string name, IndicatorRenderer renderer) =>
            Manifold.RegisterOutput(this, name, renderer);

        protected void EmitSignal(IndicatorSignal signal, string note) =>
            Manifold.OnSignal(this, signal, note);

        protected void EmitNextValue(string name, float value) =>
            Manifold.OnNextValue(this, name, value);

        protected void EmitNextValue(string name, decimal value) =>
            EmitNextValue(name, (float)value);
    }
}