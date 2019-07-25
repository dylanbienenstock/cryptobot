using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Modules
{
    [TypescriptDefine]
    public class SignalEmitter
    {
        [JavascriptBindable]
        public Action OnInit;

        [JavascriptBindable]
        public Action OnTick;
    }
}