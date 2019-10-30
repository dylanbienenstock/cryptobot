using System.Reactive.Subjects;
using CryptoBot.Exchanges;
using CryptoBot.Scripting.Modules;

namespace CryptoBot.Scripting
{
    public class PairSelectorScript : Script<PairSelector>
    {
        private PairSelector _PairSelector;
        public PairSelector PairSelector
        {
            get
            {
                if (_PairSelector == null)
                    _PairSelector = Module as PairSelector;

                return _PairSelector;
            }
        }

        public Subject<Market> OnEnableMarket;
        public Subject<Market> OnDisableMarket;

        public PairSelectorScript(ScriptContext context) : base(context) => Initialize();
        public PairSelectorScript(string tsSource, ScriptInputs inputs, Market market = null) 
            : base(tsSource, inputs, market) => Initialize();

        public new PairSelectorScript Clone(Market market = null) => 
            new PairSelectorScript(Context.Clone(market));

        private void Initialize()
        {
            OnEnableMarket = new Subject<Market>();
            OnDisableMarket = new Subject<Market>();
        }

        public override void OnDisposed()
        {
            if (OnEnableMarket != null) OnEnableMarket.Dispose();
            if (OnDisableMarket != null) OnDisableMarket.Dispose();
        }
    }
}