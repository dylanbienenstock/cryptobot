using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Modules
{
    [TypescriptDefine("SignalEmitter")]
    public class SignalEmitter : Module
    {
        [JavascriptBindable]
        public Action OnTrade;
    }
}