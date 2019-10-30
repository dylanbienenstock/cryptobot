using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Indicators;
using CryptoBot.Scripting.Modules;

namespace CryptoBot.Scripting
{
    public static class ScriptManager
    {
        public static ExchangeNetwork Network;
        public static IndicatorManifold Indicators;
        
        public static void Initialize(ExchangeNetwork network, IndicatorManifold indicators)
        {
            Network = network;
            Indicators = indicators;
        }

        public static async Task<ScriptInputs> GetRequiredInputs<T>(string tsSource) where T : Module
        {
            using (var script = new Script<T>(tsSource, ScriptInputs.Empty))
            {
                script.Context.SuppressIndicatorRequirements = true;
                await script.Execute();
                return script.Context.Inputs;
            }
        }

        public static async void Execute(ModuleType moduleType, string tsSource)
        {
            var market = Network.GetMarket("Binance", "BTC/USDT", true);
            IScript script = null;

            switch (moduleType)
            {
                case ModuleType.Anonymous:
                    script = new Script<Module>(tsSource, ScriptInputs.Empty, market);
                    break;
                case ModuleType.PairSelector:
                    script = new PairSelectorScript(tsSource, ScriptInputs.Empty, market);
                    break;
                case ModuleType.SignalEmitter:
                    script = new SignalEmitterScript(tsSource, ScriptInputs.Empty, market);
                    break;
                case ModuleType.OrderManager:
                    script = new OrderManagerScript(tsSource, ScriptInputs.Empty, market);
                    break;
            }

            script.ListenForMessages();
            await script.Execute();
        }
    }
}