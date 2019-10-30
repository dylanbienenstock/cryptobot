using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Scripting.Modules;
using System.Reactive.Linq;
using CryptoBot.Scripting.Library;
using Jint.Runtime;
using Jint.Native.Object;

namespace CryptoBot.Scripting
{
    public class Script<T> : IScript, IDisposable where T : Module
    {
        public bool Disposed { get; private set; }
        public ScriptContext Context;
        public T Module;
        public ObjectInstance ModuleObject;
        public string Name;

        private IDisposable _messageSub;

        public Script(string tsSource, ScriptInputs inputs, Market market = null)
        {
            var moduleType = GetModuleType();
            Context = new ScriptContext(tsSource, moduleType, inputs, market);
            Context.Script = this;
            Disposed = false;
        }

        public Script(ScriptContext context)
        {
            var moduleType = GetModuleType();
            Context = context;
            Context.Script = this;
            Disposed = false;
        }

        public Script<T> Clone(Market market = null) =>
            new Script<T>(Context.Clone(market));

        public void Dispose()
        {
            if (Disposed) return;

            Disposed = true;
            Context.Dispose();

            if (_messageSub != null)
                _messageSub.Dispose();

            foreach (var lease in Context.IndicatorLeases)
                ScriptManager.Indicators.RevokeLease(lease);

            OnDisposed();
        }

        public virtual void OnDisposed() { }

        public void ListenForMessages()
        {
            _messageSub = Context.OnMessage.Subscribe(message =>
            {
                ConsoleLibrary.WriteMessage(Context, message);
            });
        }

        public async Task Execute()
        {
            try
            {
                await JavascriptHost.Execute(Context);
                InstantiateModule();
                ReadDefaultExportName();
                OnExecuted();
                Module.OnInit();
            }
            catch (JavaScriptException ex)
            {
                JavascriptHost.HandleRuntimeException(Context, ex);
            }
        }

        public virtual void OnExecuted() { }

        private void InstantiateModule()
        {
            ModuleObject = Context.Engine.Execute("new exports.default()")
                .GetCompletionValue()
                .AsObject();
            Context.ModuleObject = ModuleObject;
            Module = ObjectBinder.BindModule<T>(Context, ModuleObject);
        }

        private void ReadDefaultExportName()
        {
            var nameRegex = new Regex(@"export(?:\s+)default(?:\s+)class(?:\s+)(.+)(?:\s+){", RegexOptions.ECMAScript);
            Name = nameRegex.Match(Context.TypescriptSource).Groups[1].ToString();
        }

        private ModuleType GetModuleType() =>
            typeof(T) == typeof(PairSelector)
                ? ModuleType.PairSelector
                : (typeof(T) == typeof(SignalEmitter)
                    ? ModuleType.SignalEmitter
                    : ModuleType.OrderManager);
    }
}