using System;

namespace CryptoBot.Scripting.Typings
{
    public class TypescriptDefine : Attribute
    {
        public string Name;
        public bool Instanced;

        public TypescriptDefine(string name = null, bool instanced = false)
        {
            Name = name;
            Instanced = instanced;
        }
    }
}