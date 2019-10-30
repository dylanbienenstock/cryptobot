using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Modules
{
    [TypescriptDefine]
    public abstract class Module
    {
        [JavascriptBindable]
        public Action OnInit;
    }
}