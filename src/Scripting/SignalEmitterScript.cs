using System.Reactive.Subjects;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Scripting.Modules;

namespace CryptoBot.Scripting
{
    public class SignalEmitterScript : Script<SignalEmitter>
    {
        private SignalEmitter _SignalEmitter;
        public SignalEmitter SignalEmitter
        {
            get
            {
                if (_SignalEmitter == null)
                    _SignalEmitter = Module as SignalEmitter;

                return _SignalEmitter;
            }
        }

        public Subject<double> OnSignal;

        private AnonymousStatisticalSeriesReader<CurrencyTrade> _tradeReader;

        public SignalEmitterScript(ScriptContext context) : base(context) => Initialize();
        public SignalEmitterScript(string tsSource, ScriptInputs inputs, Market market = null)
            : base(tsSource, inputs, market) => Initialize();

        private void Initialize()
        {
            OnSignal = new Subject<double>();
        }

        public new SignalEmitterScript Clone(Market market = null) => 
            new SignalEmitterScript(Context.Clone(market));

        public override void OnExecuted()
        {
            ListenForTrades();
        }

        public override void OnDisposed()
        {
            if (OnSignal != null)
                OnSignal.Dispose();

            if (_tradeReader != null)
                _tradeReader.Dispose();
        }

        public void EmitSignal(double signal)
        {
            OnSignal.OnNext(signal);
        }

        private void ListenForTrades()
        {
            if (Context.Market == null) return;

            var signalEmitter = Module as SignalEmitter;

            _tradeReader = Context.Market.Trades.BindAnonymousReader
            (
                OnFinalizeRecord: _ => signalEmitter.OnTrade()
            );
        }
    }
}