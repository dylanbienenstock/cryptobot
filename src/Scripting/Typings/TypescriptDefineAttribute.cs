using System;

namespace CryptoBot.Scripting.Typings
{
    public class TypescriptDefine : Attribute
    {
        public string Name;
        public string Namespace;
        public bool Instanced;
        public bool DefineConstructor;

        public TypescriptDefine
        (
            string name = null,
            string @namespace = null,
            bool instanced = false,
            bool defineConstructor = false
        )
        {
            Name = name;
            Namespace = @namespace;
            Instanced = instanced;
            DefineConstructor = defineConstructor;
        }
    }
}