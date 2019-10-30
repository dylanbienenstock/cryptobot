using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CryptoBot.Scripting.Typings
{
    public class FunctionDefinition
    {
        public string Name;
        public string ReturnType;
        public List<TypedIdentifier> Parameters;

        public readonly TypescriptDocumentation Comment;
        public readonly List<TypescriptDocumentation.Parameter> ParameterComments;

        public FunctionDefinition(string name, List<TypedIdentifier> parameters = null)
        {
            Name = name.Split('`')[0];
            ReturnType = "void";
            Parameters = parameters ?? new List<TypedIdentifier>();
        }

        public FunctionDefinition(MethodInfo method)
        {
            Name = method.Name;
            ReturnType = PrimitiveTypes.NameOf(method.ReturnType, true);
            Parameters = method.GetParameters()
                .Select(p => new TypedIdentifier(p.Name, p.ParameterType))
                .ToList();

            Comment = (TypescriptDocumentation)method.GetCustomAttribute(typeof(TypescriptDocumentation));
            ParameterComments = method.GetCustomAttributes(typeof(TypescriptDocumentation.Parameter), false)
                .Select(attr => (TypescriptDocumentation.Parameter)attr)
                .ToList();
        }

        public FunctionDefinition(ConstructorInfo constructor)
        {
            Name = "constructor";
            ReturnType = PrimitiveTypes.NameOf(constructor.ReflectedType, true);
            Parameters = constructor.GetParameters()
                .Select(p => new TypedIdentifier(p.Name, p.ParameterType))
                .ToList();

            Comment = (TypescriptDocumentation)constructor.GetCustomAttribute(typeof(TypescriptDocumentation));
            ParameterComments = constructor.GetCustomAttributes(typeof(TypescriptDocumentation.Parameter), false)
                .Select(attr => (TypescriptDocumentation.Parameter)attr)
                .ToList();

            // Map options type based on TypescriptOptionsType attribute
            var optionsTypeAttr = (TypescriptOptionsType)constructor.GetCustomAttribute(typeof(TypescriptOptionsType));

            if (optionsTypeAttr == null) return;
            if (Parameters.Count != 1 || Parameters[0].Name != "options")
                throw new Exception("Constructors with the TypescriptOptionsType attribute must contain only one parameter, named 'options'.");

            TypescriptDefinitions.DefineClass(optionsTypeAttr.OptionsType);
            Parameters[0] = new TypedIdentifier("options", optionsTypeAttr.OptionsType);
        }

        public string GetDocumentation()
        {
            string documentation = "";
            string start = "    /**\n";
            string end =   " */";

            void StartComment()
            {
                if (!documentation.StartsWith(start))
                    documentation = start;
            }

            void EndComment()
            {
                if (!documentation.StartsWith(start)) return;

                documentation += end;
                documentation = documentation
                    .Replace("\n", "\n    ")
                    .TrimEnd();
                documentation += "\n";
            }

            void Write(string comment)
            {
                StartComment();
                documentation += comment + "\n";
            }

            if (Comment != null)
                Write(Comment.ToString());

            if (ParameterComments != null && ParameterComments.Count > 0)
            {
                foreach (var parameterComment in ParameterComments)
                    Write(parameterComment.ToString());
            }

            EndComment();

            return documentation;
        }

        public override string ToString()
        {
            var parameters = $"{String.Join(", ", Parameters.Select(p => p.ToString()))}";
            return $"{GetDocumentation()}    {TypescriptDefinitions.CamelCase(Name)}({parameters}): {ReturnType}";
        }
    }
}