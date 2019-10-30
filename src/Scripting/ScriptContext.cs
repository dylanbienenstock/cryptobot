using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using CryptoBot.Exchanges;
using CryptoBot.Indicators;
using CryptoBot.Scripting.Modules;
using Jint;
using Jint.Native.Object;

namespace CryptoBot.Scripting
{
    public class ScriptContext
    {
        public Engine Engine;
        public dynamic Script;
        public ModuleType ModuleType;
        public string JavascriptSource;
        public ObjectInstance ModuleObject;
        public bool SuppressIndicatorRequirements;

        public string       TypescriptSource { get; private set; }
        public string       InstanceId       { get; private set; }
        public bool         Connected        { get; private set; }
        public bool         Disposed         { get; private set; }
        public ScriptInputs Inputs           { get; private set; }
        public Market       Market           { get; private set; }
        
        public List<IndicatorLease> IndicatorLeases { get; private set; }

        public string GenericSymbol => Market != null ? Market.Pair.ToGenericSymbol() : "null";
        public string ExchangeName  => Market != null ? Market.Exchange.Name          : "null";

        public Subject<string>             OnVerbose;
        public Subject<string>             OnLog;
        public Subject<string>             OnWarn;
        public Subject<string>             OnError;
        public Subject<(LogLevel, string)> OnMessage;

        public ScriptContext(string tsSource, ModuleType moduleType, ScriptInputs inputs, Market market = null)
        {
            TypescriptSource = tsSource;
            ModuleType       = moduleType;
            InstanceId       = Guid.NewGuid().ToString();
            Inputs           = inputs;
            Market           = market;
            
            IndicatorLeases = new List<IndicatorLease>();

            OnVerbose = new Subject<string>();
            OnLog     = new Subject<string>();
            OnWarn    = new Subject<string>();
            OnError   = new Subject<string>();
            OnMessage = new Subject<(LogLevel, string)>();
        }

        public ScriptContext Clone(Market market = null)
        {
            var clone = new ScriptContext(TypescriptSource, ModuleType, Inputs, market ?? Market);
            clone.JavascriptSource = JavascriptSource;
            return clone;
        }

        public void Dispose()
        {
            if (OnVerbose != null) OnVerbose.Dispose();
            if (OnLog != null)     OnLog.Dispose();
            if (OnWarn != null)    OnWarn.Dispose();
            if (OnError != null)   OnError.Dispose();

            Disposed = true;
        }
    }
}