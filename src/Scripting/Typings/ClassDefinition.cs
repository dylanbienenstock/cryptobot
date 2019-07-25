using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CryptoBot.Scripting.Typings
{
    public class ClassDefinition
    {
        public readonly Type                       Type;
        public readonly string                     Name;
        public readonly Dictionary<string, string> MemberAliases;
        public readonly List<FunctionDefinition>   Functions;
        public readonly List<TypedIdentifier>      Properties;
        public readonly bool                       Instanced;
        public readonly bool                       Temporary;

        public ClassDefinition BaseTypeDefinition;

        public string Key {
            get
            {
                string key = Name;
                var path = this;

                while (path.BaseTypeDefinition != null) {
                    key = $"{path.BaseTypeDefinition.Name}->{key}";
                    path = path.BaseTypeDefinition;
                }

                return key;
            }
        }
        
        public ClassDefinition(Type type)
        {
            Console.WriteLine(type.Name);
            var attribute = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));

            MemberAliases = new Dictionary<string, string>();
            BaseTypeDefinition = TypescriptDefinitions.GetClassDefinition(type.BaseType);
            Name = attribute.Name ?? type.Name;
            Instanced = attribute.Instanced;

            var bindingFlags = BindingFlags.Public | 
                BindingFlags.DeclaredOnly | 
                BindingFlags.Instance;

            bool shouldIgnore(dynamic _type)
            {
                return ((IEnumerable<dynamic>)_type.GetCustomAttributes(true))
                    .Any(a => a.GetType() == typeof(TypescriptIgnore));
            }

            foreach (var nestedType in type.GetNestedTypes().Where(t => !shouldIgnore(t)))
                TypescriptDefinitions.DefineClass(nestedType);

            bool shouldDefineFunction(MethodInfo function)
            {
                if (shouldIgnore(function)) return false;
                if (function.Name.StartsWith("get_")) return false;

                if (BaseTypeDefinition != null)
                {
                    if (BaseTypeDefinition.Functions.Any(f => f.Name == function.Name))
                        return false;
                }

                if (function.GetParameters().Any(p => !TypescriptDefinitions.IsDefined(p.ParameterType) &&
                    !PrimitiveTypes.IsPrimitive(p.ParameterType)))
                        return false;

                return TypescriptDefinitions.IsDefined(function.ReturnType) ||
                    PrimitiveTypes.IsPrimitive(function.ReturnType);
            }

            bool shouldDefineProperty(string propertyName, Type propertyType)
            {
                if (BaseTypeDefinition != null)
                {
                    if (BaseTypeDefinition == null) return true;
                    if (BaseTypeDefinition.Properties.Any(p => p.Name == propertyName))
                        return false;
                }

                return TypescriptDefinitions.IsDefined(propertyType) ||
                    PrimitiveTypes.IsPrimitive(propertyType);
            }

            bool shouldDefineAction(FieldInfo field, out JavascriptBindable bindableAttribute)
            {
                bindableAttribute = (JavascriptBindable)field.GetCustomAttribute(typeof(JavascriptBindable));
                return bindableAttribute != null;
            }

            Functions = type.GetMethods(bindingFlags)
                .Where(f => shouldDefineFunction(f))
                .Select(f =>
                {
                    MemberAliases[TypescriptDefinitions.CamelCase(f.Name)] = f.Name;
                    return new FunctionDefinition(f);
                })
                .ToList();

            Properties = type.GetProperties(bindingFlags)
                .Where(p => !shouldIgnore(p) && shouldDefineProperty(p.Name, p.PropertyType))
                .Select(p => 
                {
                    MemberAliases[TypescriptDefinitions.CamelCase(p.Name)] = p.Name;
                    return new TypedIdentifier(p.Name, p.PropertyType);
                })
                .Concat
                (
                    type.GetFields()
                        .Where(f => !shouldIgnore(f) && shouldDefineProperty(f.Name, f.FieldType))
                        .Select(f => 
                        {
                            MemberAliases[TypescriptDefinitions.CamelCase(f.Name)] = f.Name;
                            return new TypedIdentifier(f.Name, f.FieldType);
                        })
                )
                .ToList();
            
            type.GetFields(bindingFlags).ToList()
                .ForEach(f => 
                {
                    if (!shouldDefineAction(f, out var bindableAttribute)) return;
                    var functionDefinition = new FunctionDefinition(f.Name);
                    Functions.Add(functionDefinition);
                    MemberAliases[TypescriptDefinitions.CamelCase(f.Name)] = f.Name;
                });
        }

        public override string ToString()
        {
            var empty = (new IEnumerable<dynamic>[] { Functions, Properties })
                .All(x => x.Count() == 0);

            var declarationType = Instanced ? "const" : "interface";

            var output = $"declare {declarationType} {Name}";

            if (BaseTypeDefinition != null)
            {
                if (!Instanced) output += $" extends {BaseTypeDefinition.Name}";
                else
                {
                    output += $": {BaseTypeDefinition.Name} & { '{' }";
                }
            }
            else if (Instanced) output += ": {";
            else output += " {";

            foreach (var function in Functions)
                output += $"\n    {function.ToString()};";

            foreach (var property in Properties)
                output += $"\n    {property.ToString()};";

            if (!empty) output += "\n";
            else        output += " ";

            output += "}";

            return output;
        }
    }
}