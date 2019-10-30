using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CryptoBot.Exchanges;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine]
    public class PairSelectionContext
    {
        private PairSelectorLibrary _library;
        private IEnumerable<Market> _markets;

        public PairSelectionContext(PairSelectorLibrary library, IEnumerable<Market> markets)
        {
            _library = library;
            _markets = markets;
        }

        public PairSelectionContext SelectExchange(string exchange)
        {
            IEnumerable<Market> markets = null;

            if (exchange.Contains("*"))
            {
                var regex = new Regex($"^{Regex.Escape(exchange).Replace("\\*", ".*")}$");
                markets = _markets.Where(m => regex.IsMatch(m.Exchange.Name));
            }
            else markets = _markets.Where(m => m.Exchange.Name == exchange);
            
            return new PairSelectionContext(_library, markets);
        }

        public PairSelectionContext SelectPair(string pair)
        {
            IEnumerable<Market> markets = null;

            if (pair.Contains("*"))
            {
                var regex = new Regex($"^{Regex.Escape(pair).Replace("\\*", ".*")}$");
                markets = _markets.Where(m => regex.IsMatch(m.Pair.ToGenericSymbol()));
            }
            else markets = _markets.Where(m => m.Pair.ToGenericSymbol() == pair);
            
            return new PairSelectionContext(_library, markets);
        }

        public void Enable()
        {
            foreach (var market in _markets)
                _library.EnableRaw(market);
        }

        public void Disable()
        {
            foreach (var market in _markets)
                _library.DisableRaw(market);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled) Enable();
            else Disable();
        }
    }
}