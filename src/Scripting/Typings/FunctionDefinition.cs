using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CryptoBot.Scripting.Typings
{
    public class FunctionDefinition
    {
        public string                Name;
        public string                ReturnType;
        public List<TypedIdentifier> Parameters;

        public FunctionDefinition(string name)
        {
            Name = name;
            ReturnType = "void";
            Parameters = new List<TypedIdentifier>();
        }

        public FunctionDefinition(MethodInfo method)
        {
            Name = method.Name;
            ReturnType = PrimitiveTypes.NameOf(method.ReturnType, true);
            Parameters = method.GetParameters()
                .Select(p => new TypedIdentifier(p.Name, p.ParameterType))
                .ToList();
        }

        public override string ToString() =>
            $"{TypescriptDefinitions.CamelCase(Name)}({String.Join(',', Parameters.Select(p => p.ToString()))}): {ReturnType}";
    }
}