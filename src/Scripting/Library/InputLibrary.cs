using System;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__input", instanced: true)]
    public class InputLibrary : InstancedLibrary
    {
        public InputLibrary(ScriptContext context) : base(context) { }
        
        public double RequireRangeInput(string key, string label, bool integer, double min, double max, double @default)
        {
            return Context.Inputs.RequireRangeInput(key, label, integer, min, max, @default);
        }
    }
}