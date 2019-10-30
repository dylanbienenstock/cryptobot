using System;
using System.Linq;
using System.Reactive.Subjects;
using CryptoBot.Scripting.Modules;
using CryptoBot.Scripting.Typings;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace CryptoBot.Scripting.Library
{
    public static class JavascriptLibrary
    {
        public static void Apply(Engine engine, ScriptContext context)
        {
            AddConstants(engine, context);

            AddInstantiableClass<Order>(engine, context);

            AddLibrary<ConsoleLibrary>(engine, context);
            AddLibrary<InputLibrary>(engine, context);
            AddLibrary<IndicatorLibrary>(engine, context);
            AddLibrary<IntervalLibrary>(engine, context);

            switch (context.ModuleType)
            {
                case ModuleType.PairSelector:
                    AddLibrary<PairSelectorLibrary>(engine, context);
                    break;
                case ModuleType.SignalEmitter:
                    AddLibrary<SignalEmitterLibrary>(engine, context);
                    break;
                case ModuleType.OrderManager:
                    AddLibrary<OrderManagerLibrary>(engine, context);
                    break;
            }
        }

        private static void AddConstants(Engine engine, ScriptContext context)
        {
            engine.SetValue("INSTANCE_ID",       context.InstanceId);
            engine.SetValue("INSTANCE_EXCHANGE", context.ExchangeName);
            engine.SetValue("INSTANCE_PAIR",     context.GenericSymbol);

            var exchanges = ScriptManager.Network.Exchanges.Select(e => new
            {
                name = e.Name,
                pairs = e.Markets.Select(m => m.Key.ToGenericSymbol())
            });

            var exchangesJson = Newtonsoft.Json.JsonConvert.SerializeObject(exchanges);

            engine.Execute($"var EXCHANGES = {exchangesJson};");
        }

        private static void AddLibrary<T>(Engine engine, ScriptContext context) where T : InstancedLibrary
        {
            var name = typeof(T).Name;
            var definition = TypescriptDefinitions.GetClassDefinition(typeof(T));

            if (definition != null) 
                name = definition.Name;

            var instance = Activator.CreateInstance(typeof(T), context);
            engine.SetValue(name, instance);
        }

        private static void AddInstantiableClass<T>(Engine engine, ScriptContext context)
        {
            engine.SetValue(typeof(T).Name, TypeReference.CreateTypeReference(engine, typeof(T)));
        }
    }
}