using System;

namespace CryptoBot.Scripting.Typings
{
    public class TypescriptOptionsType : Attribute
    {
        public Type OptionsType;

        public TypescriptOptionsType(Type optionsType)
        {
            OptionsType = optionsType;
        }
    }
}