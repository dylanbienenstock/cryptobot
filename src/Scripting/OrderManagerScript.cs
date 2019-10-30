using CryptoBot.Exchanges;
using CryptoBot.Scripting.Modules;

namespace CryptoBot.Scripting
{
    public class OrderManagerScript : Script<OrderManager>
    {
        private OrderManager _OrderManager;
        public OrderManager OrderManager
        {
            get
            {
                if (_OrderManager == null)
                    _OrderManager = Module as OrderManager;

                return _OrderManager;
            }
        }

        public OrderManagerScript(ScriptContext context) : base(context) => Initialize();
        public OrderManagerScript(string tsSource, ScriptInputs inputs, Market market = null) 
            : base(tsSource, inputs, market) => Initialize();
    
        public new OrderManagerScript Clone(Market market = null) => 
            new OrderManagerScript(Context.Clone(market));

        private void Initialize()
        {

        }
    }
}