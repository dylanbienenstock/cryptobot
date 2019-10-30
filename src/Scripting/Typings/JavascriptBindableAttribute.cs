using System;
using System.Linq;

namespace CryptoBot.Scripting.Typings
{
    public class JavascriptBindable : Attribute
    {
        public string[] ParameterNames;

        public JavascriptBindable(params string[] parameterNames)
        {
            ParameterNames = parameterNames;
        }
    }
}