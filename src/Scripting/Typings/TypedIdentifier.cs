using System;

namespace CryptoBot.Scripting.Typings
{
    public class TypedIdentifier
    {
        public readonly string Name;
        public readonly string Type;

        public TypedIdentifier(string name, Type type)
        {
            Name = name;
            Type = PrimitiveTypes.NameOf(type, true);
        }

        public override string ToString() => $"{TypescriptDefinitions.CamelCase(Name)}: {Type}";
    }
}