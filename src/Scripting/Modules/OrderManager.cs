using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Modules
{
    public class OrderManager : Module
    {
        [JavascriptBindable]
        public Action OnDisabled;

        [JavascriptBindable("signal")]
        public Action<double> OnSignal;
    }
}