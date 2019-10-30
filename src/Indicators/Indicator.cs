using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Reactive.Subjects;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Indicators
{
    [TypescriptDefine(@namespace: "InstanceOf")]
    public abstract class Indicator : TradingPeriodSeriesReader
    {
        public abstract IndicatorDetails Details { get; }
        public string Id;
        public string Name => Details.Name;
        public IndicatorType Type => Details.Type;
        public long TimeFrame;
        [TypescriptIgnore]
        public bool UpToDate;
        [TypescriptIgnore]
        public ExpandoObject Settings;
        public Market Market;
        public IndicatorManifold Manifold;
        public IndicatorDataAggregate DataAggregate;
        public TradingPeriodSeries Input;
        public Subject<IndicatorOutput> Output;

        public void Initialize(IndicatorManifold manifold, Market market, long timeFrame, ExpandoObject settings)
        {
            Manifold      = manifold;
            Market        = market;
            TimeFrame     = timeFrame;
            Settings      = settings;

            Id            = Guid.NewGuid().ToString();
            DataAggregate = new IndicatorDataAggregate(timeFrame);
            Output        = new Subject<IndicatorOutput>();

            Configure(settings);
        }

        [TypescriptIgnore]
        public void Dispose()
        {
            Source.UnbindReader(this);
        }

        [TypescriptIgnore]
        public IndicatorDataAggregate ProcessOutsideManifold(long timeFrame, List<HistoricalTradingPeriod> periods)
        {
            var manifold = new IndicatorManifold();
            manifold.AddMarket(Market);
            var clone = manifold.CreateRaw(Market, Name, timeFrame, Settings);

            foreach (var field in clone.DataAggregate.Fields)
                field.Value.Values.Duration = TimeSpan.FromMilliseconds((double)timeFrame * (double)periods.Count);

            manifold.AddTradingPeriods(clone, periods);

            return clone.DataAggregate;
        }

        [TypescriptIgnore]
        public abstract void Configure(dynamic settings);

        protected TradingPeriodSeries RequireInput(long timeFrame, int periods) =>
            Manifold.RequireInput(this, timeFrame, periods);

        protected void PrimaryOutputField(string name, IndicatorRenderer renderer = null) =>
            DataAggregate.AddPrimaryField(name, renderer);

        protected void OutputField(string name, IndicatorRenderer renderer = null) =>
            DataAggregate.AddField(name, renderer);

        protected void EmitSignal(IndicatorSignal signal, string note) =>
            Manifold.OnSignal(this, signal, note);

        protected void EmitNextValue(Dictionary<string, object> nextValue) =>
            Manifold.OnNextValue(this, nextValue);

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

        [TypescriptCustomDefinition]
        public string DefineSettingsAndDecorator()
        {
            if (Details.Settings.Length == 0) return "";

            var indicatorName = GetType().Name;
            var settingsInterfaceName = $"{indicatorName}Settings";
            var def = $"declare interface {settingsInterfaceName} {'{'}\n";
            def += "    timeFrame: TimeFrame;\n";

            Details.Settings.ToList().ForEach(setting =>
            {
                def += "    ";
                def += TypescriptDefinitions.CamelCase(setting.Key);
                def += ": ";
                def += setting.TypescriptType;
                def += ";\n";
            });

            def += $@"{'}'}
            
namespace Indicator {'{'}
    export function {indicatorName}(settings: {settingsInterfaceName}): InstanceOf.{indicatorName} {'{'}
        return __requireIndicator<InstanceOf.{indicatorName}>({'"'}{indicatorName}{'"'}, settings);
    {'}'}

    export namespace Multi {'{'}
        export function {indicatorName}(settings: {settingsInterfaceName}): IndicatorMultiInstance<InstanceOf.{indicatorName}> {'{'}
            return __requireMultiIndicator<InstanceOf.{indicatorName}>({'"'}{indicatorName}{'"'}, settings);
        {'}'}
    {'}'}
{'}'}";

            return def;
        }
    }
}