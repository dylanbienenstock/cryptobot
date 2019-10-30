using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Dynamic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using Telegram.Bot.Types;

namespace CryptoBot.Indicators
{
    public class IndicatorManifold
    {
        private static Rectangle _renderBounds = new Rectangle(0, 0, 1024, 396);
        private static Color _renderBackground = Color.FromArgb(255, 48, 48, 48);
        
        public ExchangeNetwork                                           Network;
        public Dictionary<Market, List<Indicator>>                       Indicators;
        public Dictionary<Indicator, IndicatorSignal>                    Signals;
        public Dictionary<Indicator, string>                             Notes;
        public Dictionary<Indicator, List<IndicatorLease>>               Leases;
        private Dictionary<Market, Dictionary<long, Dictionary<int, TradingPeriodSeries>>> _inputs;
        private Dictionary<Market, DateTime>                             _historyTime;

        public IndicatorManifold(ExchangeNetwork network = null)
        {
            Network          = network;
            Indicators       = new Dictionary<Market, List<Indicator>>();
            Signals          = new Dictionary<Indicator, IndicatorSignal>();
            Notes            = new Dictionary<Indicator, string>();
            Leases           = new Dictionary<Indicator, List<IndicatorLease>>();

            _inputs          = new Dictionary<Market, Dictionary<long, Dictionary<int, TradingPeriodSeries>>>();
            _historyTime     = new Dictionary<Market, DateTime>();

            if (Network != null)
            {
                foreach (var exchange in Network.Exchanges)
                {
                    foreach (var market in exchange.Markets.Values)
                        AddMarket(market);
                }

                Task.Run(() => {
                    SpinWait.SpinUntil(() => Network.MergedTradeStream != null);

                    Network.MergedTradeStream.Subscribe
                    (
                        onNext: trade => OnTrade(trade),
                        onError: ex => throw ex,
                        onCompleted: () => {}
                    );
                });
            }
        }

        public void AddMarket(Market market)
        {
            lock (_inputs)
            {
                Indicators[market] = new List<Indicator>();
                _inputs[market] = new Dictionary<long, Dictionary<int, TradingPeriodSeries>>();
            }
        }

        public void AddTradingPeriods(Indicator indicator, List<HistoricalTradingPeriod> periods)
        {
            lock (_inputs)
            {
                indicator.Source.ReaderFilter = indicator;
                
                for (int i = 0; i < periods.Count; i++)
                {
                    _historyTime[indicator.Market] = periods[i].Time;
                    indicator.Input.Add(periods[i]);
                }

                indicator.Source.ReaderFilter = null;
            }
        }

        public void OnTrade(CurrencyTrade trade)
        {
            lock (_inputs)
            {
                var inputs = _inputs[trade.Market]
                    .SelectMany(kv => kv.Value.Values)
                    .ToArray();

                for (int i = 0; i < inputs.Length; i++)
                    inputs[i].Source.Record(trade);
            }
        }

        public async Task<IndicatorLease> GetIndicator
        (
            Market market,
            string indicatorName,
            long timeFrame,
            ExpandoObject settings,
            bool getHistory = true
        )
        {
            if (!Indicators.ContainsKey(market))
                AddMarket(market);

            // Attempts to find an existing indicator by comparing their type and settings
            var newSettings = (IDictionary<string, object>)settings;
            var indicator = Indicators[market].FirstOrDefault(ind =>
            {
                if (ind.GetType() != IndicatorList.GetIndicatorType(indicatorName)) return false;
                if (ind.TimeFrame != timeFrame) return false;
                var compSettings = (IDictionary<string, object>)ind.Settings;
                if (compSettings.Keys.Count == 0 && settings.Count() == 0) return true;
                return newSettings.All(kv => compSettings[kv.Key].Equals(kv.Value));
            });

            if (indicator == null)
            {
                indicator = CreateRaw(market, indicatorName, timeFrame, settings);

                if (getHistory)
                {
                    var endTime = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
                    endTime = Math.Ceiling(endTime / (double)timeFrame) * (double)timeFrame;
                    var startTime = endTime - (double)timeFrame * 250d;
                    var periods = await Network.GetTradingPeriods(market, startTime, endTime, timeFrame);

                    AddTradingPeriods(indicator, periods);

                    indicator.UpToDate = true;
                }
            }

            return CreateLease(indicator);
        }

        public IndicatorLease CreateLease(Indicator indicator)
        {
            if (!Leases.ContainsKey(indicator))
                Leases[indicator] = new List<IndicatorLease>();

            var lease = new IndicatorLease(indicator);
            Leases[indicator].Add(lease);

            return lease;
        }

        public void RevokeLease(IndicatorLease lease)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("REVOKED LEASE!");
            Console.ResetColor();

            if (Leases[lease.Indicator] == null)
                throw new Exception("Tried to revoke an extraneous lease");

            if (!Leases[lease.Indicator].Contains(lease))
                throw new Exception("Tried to revoke an already revoked lease");

            Leases[lease.Indicator].Remove(lease);

            if (Leases[lease.Indicator].Count > 0) return;

            lease.Indicator.Dispose();
            Leases.Remove(lease.Indicator);
            Indicators[lease.Indicator.Market].Remove(lease.Indicator);
            Signals.Remove(lease.Indicator);
            Notes.Remove(lease.Indicator);

            RemoveInputIfUnused(lease);

            if (Indicators[lease.Indicator.Market].Count == 0)
                Indicators.Remove(lease.Indicator.Market);

                // TODO: REMOVE INPUTS
        }

        private void RemoveInputIfUnused(IndicatorLease lease)
        {
            var usedByAnotherIndicator = false;

            foreach (var indicatorList in Indicators.Values)
            {
                foreach (var indicator in indicatorList)
                {
                    if (indicator.Input == lease.Indicator.Input)
                    {
                        usedByAnotherIndicator = true;
                        break;
                    }
                }

                if (usedByAnotherIndicator) break;
            }

            if (!usedByAnotherIndicator)
            {
                _inputs[lease.Indicator.Market][lease.Indicator.TimeFrame].Remove(lease.Indicator.Input.Periods);

                if (_inputs[lease.Indicator.Market][lease.Indicator.TimeFrame].Count == 0)
                    _inputs[lease.Indicator.Market].Remove(lease.Indicator.TimeFrame);
            }
        }

        public Indicator CreateRaw(Market market, string indicatorName, long timeFrame, ExpandoObject settings)
        {
            lock (_inputs)
            {
                Type type = IndicatorList.GetIndicatorType(indicatorName);
                Indicator indicator = (Indicator)Activator.CreateInstance(type);
                indicator.Initialize(this, market, timeFrame, settings);

                Signals[indicator] = IndicatorSignal.Neutral;
                Notes[indicator] = "Neutral";
                Indicators[market].Add(indicator);

                return indicator;
            }
        }

        public TradingPeriodSeries RequireInput(Indicator indicator, long timeFrame, int periods)
        {
            lock (_inputs)
            {
                if (!_inputs[indicator.Market].ContainsKey(timeFrame))
                    _inputs[indicator.Market][timeFrame] = new Dictionary<int, TradingPeriodSeries>();
                
                if (!_inputs[indicator.Market][timeFrame].ContainsKey(periods))
                {
                    _inputs[indicator.Market][timeFrame][periods] =
                        new TradingPeriodSeries(indicator.Market.Trades, timeFrame, periods);
                }

                indicator.Input = _inputs[indicator.Market][timeFrame][periods];
            }

            return _inputs[indicator.Market][timeFrame][periods];
        }

        public void OnSignal(Indicator indicator, IndicatorSignal signal, string note)
        {
            if (Signals[indicator] == signal) return;
            if (!indicator.UpToDate) return;

            Signals[indicator] = signal;
            Notes[indicator] = note;
            
            // SendTelegramSignal(indicator);
        }

        public void OnNextValue
        (
            Indicator indicator,
            Dictionary<string, object> changes
        )
        {
            if (!_historyTime.ContainsKey(indicator.Market)) return;
            if (indicator.DataAggregate.PrimaryField == null) return;

            var currentTime = DateTime.UtcNow.Quantize(indicator.TimeFrame);
            var historyTime = _historyTime[indicator.Market].Quantize(indicator.TimeFrame);
            var time        = indicator.UpToDate ? currentTime : historyTime;
            var dataAgg     = indicator.DataAggregate;
            var isUpdate    = false;

            if (dataAgg.PrimaryField.Values.Count > 0)
                isUpdate = time == dataAgg.PrimaryField.Values.TailTime;

            foreach (var change in changes)
            {
                if (isUpdate) dataAgg.UpdateTail(change.Key, change.Value);
                else          dataAgg.Record(change.Key, change.Value, time);
            }
            
            // var output = new IndicatorOutput(indicator, dataAgg);
            // indicator.Output.OnNext(output);

            var output = new IndicatorOutput(time, changes);
            indicator.Output.OnNext(output);
        }
    }
}