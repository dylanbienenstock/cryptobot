using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__orderManager", instanced: true)]
    public class OrderManagerLibrary : InstancedLibrary
    {
        private OrderManagerScript _orderManager;

        public OrderManagerLibrary(ScriptContext context) : base(context)
        {
            _orderManager = context.Script as OrderManagerScript;
        }
    }
}