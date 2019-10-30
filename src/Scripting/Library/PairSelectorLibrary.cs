using System.Linq;
using System.Text.RegularExpressions;
using CryptoBot.Exchanges;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__pairSelector", instanced: true)]
    public class PairSelectorLibrary : InstancedLibrary
    {
        private PairSelectorScript _pairSelector;

        public PairSelectorLibrary(ScriptContext context) : base(context)
        {
            _pairSelector = (PairSelectorScript)context.Script;
        }

        public PairSelectionContext Select(string exchange, string pair)
        {
            var exchanges = ScriptManager.Network.Exchanges.ToList();

            if (exchange.Contains("*"))
            {
                var regex = new Regex($"^{Regex.Escape(exchange).Replace("\\*", ".*")}$");
                exchanges = exchanges.Where(e => regex.IsMatch(e.Name)).ToList();
            }

            var markets = exchanges.SelectMany(e => e.Markets.Values);
            
            if (pair.Contains("*"))
            {
                var regex = new Regex($"^{Regex.Escape(pair).Replace("\\*", ".*")}$");
                markets = markets.Where(m => regex.IsMatch(m.Pair.ToGenericSymbol())).ToList();
            }

            return new PairSelectionContext(this, markets);
        }

        public PairSelectionContext SelectAll()
        {
            return Select("*", "*");
        }

        public PairSelectionContext SelectExchange(string exchange)
        {
            return Select(exchange, "*");
        }

        public PairSelectionContext SelectPair(string pair)
        {
            return Select("*", pair);
        }

        public void Enable(string exchange, string pair)
        {
            var market = ScriptManager.Network.GetMarket(exchange, pair, true);
            EnableRaw(market);
        }

        public void Disable(string exchange, string pair)
        {
            var market = ScriptManager.Network.GetMarket(exchange, pair, true);
            DisableRaw(market);
        }

        [TypescriptIgnore]
        public void EnableRaw(Market market) =>
            _pairSelector.OnEnableMarket.OnNext(market);

        [TypescriptIgnore]
        public void DisableRaw(Market market) =>
            _pairSelector.OnDisableMarket.OnNext(market);
    }
}