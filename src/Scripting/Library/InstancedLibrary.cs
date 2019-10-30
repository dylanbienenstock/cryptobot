using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    public class InstancedLibrary
    {
        [TypescriptIgnore]
        public ScriptContext Context;

        public InstancedLibrary(ScriptContext context)
        {
            Context = context;
        }
    }
}