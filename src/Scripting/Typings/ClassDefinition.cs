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
        public readonly string                     Namespace;
        public readonly Dictionary<string, string> MemberAliases;
        public readonly FunctionDefinition         Constructor;
        public readonly List<FunctionDefinition>   Functions;
        public readonly List<TypedIdentifier>      Properties;
        public readonly bool                       Instanced;
        public readonly bool                       DefineConstructor;
        public readonly bool                       Temporary;

        public ClassDefinition BaseTypeDefinition;

        public string Key
        {
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
            var defineAttribute = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));

            Type = type;
            MemberAliases = new Dictionary<string, string>();
            BaseTypeDefinition = TypescriptDefinitions.GetClassDefinition(type.BaseType);
            Name = defineAttribute?.Name ?? type.Name.Split('`')[0];
            Namespace = defineAttribute?.Namespace;
            Instanced = defineAttribute?.Instanced ?? false;
            DefineConstructor = defineAttribute?.DefineConstructor ?? false;

            if (DefineConstructor)
            {
                var constructors = type.GetConstructors();

                if (constructors.Length == 0)
                    throw new Exception("Cannot generate Typescript definition for a constructor whose underlying class has no constructors");

                if (constructors.Length > 1)
                    throw new Exception("Cannot generate Typescript definition for a constructor whose underlying class contains multiple constructors");

                Constructor = new FunctionDefinition(constructors[0]);
            }

            var bindingFlags = BindingFlags.Public | 
                BindingFlags.DeclaredOnly | 
                BindingFlags.Instance;

            bool shouldIgnore(dynamic _type)
            {
                return ((IEnumerable<dynamic>)_type.GetCustomAttributes(true))
                    .Any(a => a.GetType() == typeof(TypescriptIgnore));
            }

            foreach (var nestedType in type.GetNestedTypes().Where(t => !shouldIgnore(t)))
            {
                if (nestedType.IsEnum)
                    TypescriptDefinitions.DefineEnum(nestedType, true);
                else if (nestedType.IsClass)
                    TypescriptDefinitions.DefineClass(nestedType, true);
            }

            bool shouldDefineFunction(MethodInfo function)
            {
                if (function.GetCustomAttribute(typeof(TypescriptCustomDefinition)) != null) return false;
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
                    PrimitiveTypes.IsPrimitive(function.ReturnType) || 
                    function.ReturnType == Type ||
                    function.ReturnType.IsGenericType || 
                    function.ReturnType.ToString() == "T[]";
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

            Functions = type.GetMethods(bindingFlags)
                .Where(f => shouldDefineFunction(f))
                .Select(f =>
                {
                    MemberAliases[TypescriptDefinitions.CamelCase(f.Name)] = f.Name;
                    return new FunctionDefinition(f);
                })
                .ToList();
            
            type.GetFields(bindingFlags).ToList()
                .ForEach(f => 
                {
                    if (!shouldDefineAction(f, out var bindableAttribute)) return;
                    
                    FunctionDefinition functionDefinition;

                    if (bindableAttribute.ParameterNames != null)
                    {
                        var parameters = f.FieldType.GenericTypeArguments
                            .Select((paramType, i) => new TypedIdentifier(bindableAttribute.ParameterNames[i], paramType))
                            .ToList();
                        functionDefinition = new FunctionDefinition(f.Name, parameters);
                    }
                    else
                    {
                        functionDefinition = new FunctionDefinition(f.Name);
                    }

                    Functions.Add(functionDefinition);
                    MemberAliases[TypescriptDefinitions.CamelCase(f.Name)] = f.Name;
                });
        }

        private string GetCustomDefinitions()
        {
            if (Type.IsAbstract) return "";

            var definitions = "";

            foreach (var method in Type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var customDefinitionAttribute = (TypescriptCustomDefinition)method
                    .GetCustomAttribute(typeof(TypescriptCustomDefinition));
                if (customDefinitionAttribute == null) continue;

                var instance = Activator.CreateInstance(Type);
                definitions += (string)method.Invoke(instance, null);

                if (definitions.Length > 0 && !definitions.StartsWith("\n\n"))
                    definitions = "\n\n" + definitions;
            }

            foreach (var method in Type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var customDefinitionAttribute = (TypescriptCustomDefinition)method
                    .GetCustomAttribute(typeof(TypescriptCustomDefinition));
                if (customDefinitionAttribute == null) continue;

                definitions += (string)method.Invoke(null, null);

                if (definitions.Length > 0 && !definitions.StartsWith("\n\n"))
                    definitions = "\n\n" + definitions;
            }

            return definitions;
        }

        public override string ToString()
        {
            var empty = (new IEnumerable<dynamic>[] { Functions, Properties })
                .All(x => x.Count() == 0);

            var exportType = Namespace == null ? "declare" : "export";
            var declarationType = Instanced ? "const" : "interface";
            var name = Name;

            if (DefineConstructor) declarationType = "class";
            if (Type.ContainsGenericParameters) name += "<T>";

            var output = $"{exportType} {declarationType} {name}";

            if (BaseTypeDefinition != null)
            {
                if (!Instanced) output += $" extends {BaseTypeDefinition.Name} {'{'}";
                else            output += $": {BaseTypeDefinition.Name} & {'{'}";
            }
            else if (Instanced) output += ": {";
            else output += " {";

            if (DefineConstructor)
                output += $"\n{Constructor.ToString()};";

            foreach (var function in Functions)
                output += $"\n{function.ToString()};";

            foreach (var property in Properties)
                output += $"\n    {property.ToString()};";

            if (!empty) output += "\n";
            else        output += " ";

            output += "}";

            if (Namespace != null)
            {
                var lines = output.Split("\n").ToList();
                output = $"namespace {Namespace} {'{'}\n";
                lines.ForEach(l => output += $"    {l}\n");
                output += "}";
            }

            output += GetCustomDefinitions();

            return output;
        }
    }
}