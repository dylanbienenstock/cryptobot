using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Indicators;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__indicators", instanced: true)]
    public class IndicatorLibrary : InstancedLibrary
    {
        public IndicatorLibrary(ScriptContext context) : base(context) { }

        public object RequireIndicator(string indicatorName, string timeFrameName, object settings)
        {
            // Return a fake instance if suppressed
            if (Context.SuppressIndicatorRequirements)
                return Activator.CreateInstance(IndicatorList.GetIndicatorType(indicatorName));

            var mappedSettings = MapSettings(indicatorName, settings);
            var timeFrame = Exchange.GetTimeFrame(timeFrameName);
            var leaseTask = ScriptManager.Indicators
                .GetIndicator(Context.Market, indicatorName, timeFrame, mappedSettings);

            leaseTask.Wait();

            var lease = leaseTask.Result;
            Context.IndicatorLeases.Add(lease);

            return lease.Indicator;
        }

        public object RequireMultiIndicator(string indicatorName, string timeFrameName, object settings)
        {
            object CreateMultiIndicator(IEnumerable<dynamic> _indicators)
            {
                var indicatorType = IndicatorList.GetIndicatorType(indicatorName);
                var unboundType = typeof(IndicatorMultiInstance<>);
                var genericType = unboundType.MakeGenericType(indicatorType);
                return Activator.CreateInstance(genericType, _indicators);
            }

            // Return a fake multi-instance if suppressed
            if (Context.SuppressIndicatorRequirements)
                return CreateMultiIndicator(null);

            var mappedSettings = MapSettings(indicatorName, settings);
            var timeFrame = Exchange.GetTimeFrame(timeFrameName);

            var exchanges = ScriptManager.Network.Exchanges;
            var markets = exchanges.SelectMany(e => e.Markets.Values);
            var leaseTasks = markets.Select(m => ScriptManager.Indicators
                .GetIndicator(m, indicatorName, timeFrame, mappedSettings))
                .ToArray();

            Task.WaitAll(leaseTasks);

            var leases = leaseTasks.Select(lt => lt.Result);
            Context.IndicatorLeases.AddRange(leases);

            var indicators = leases.Select(l => l.Indicator);
            var multiIndicator = CreateMultiIndicator(indicators);

            return multiIndicator;
        }

        private ExpandoObject MapSettings(string indicatorName, object settings)
        {
            var indicatorDetails = IndicatorList.GetIndicatorDescription(indicatorName);
            var requiredSettings = indicatorDetails.Settings.Select(s => s.Name);
            var givenSettings    = (IDictionary<string, object>)settings;
            var mappedSettings   = (ExpandoObject)givenSettings
                .Aggregate
                (
                    seed: new ExpandoObject() as IDictionary<string, Object>,
                    func: (accum, val) => 
                    {
                        var mappedName = TypescriptDefinitions.UnCamelCase(val.Key);
                        accum[mappedName] = val.Value;
                        return accum;
                    }
                );

            return mappedSettings;
        }
    }
}