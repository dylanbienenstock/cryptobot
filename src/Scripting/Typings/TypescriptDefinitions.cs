using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace CryptoBot.Scripting.Typings
{
    public static class TypescriptDefinitions {
        public static Dictionary<Type, ClassDefinition> Classes;
        public static Dictionary<string, Dictionary<string, string>> ClassMemberAliases;

        public static string DefinitionFileDirectory 
        {
            get
            {
                var codebaseFile = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                var outputDirectory = Path.GetDirectoryName(codebaseFile);
                return Path.Join(outputDirectory, "src/Scripting/Compiler");
            }
        }
            
        public static string DefinitionFilePath => 
            Path.Join(DefinitionFileDirectory, "types.ts");

        public static string ClassMemberAliasFilePath => 
            Path.Join(DefinitionFileDirectory, "alias.ts");

        public static string ProjectFilePath => 
            Path.Join(DefinitionFileDirectory, "tsconfig.json");

        static TypescriptDefinitions()
        {
            Classes = new Dictionary<Type, ClassDefinition>();
            ClassMemberAliases = new Dictionary<string, Dictionary<string, string>>();

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attr = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
                if (attr != null) DefineClass(type);
            }

            Console.WriteLine("GENERATED TYPEDEFS:");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Generate() + "\n");
            Console.ResetColor();
        }

        public static void DefineClass(Type type)
        {
            var attr = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
            if (attr == null) return;

            if (PrimitiveTypes.IsPrimitive(type)) return;
            if (Classes.ContainsKey(type)) return;
            Classes[type] = new ClassDefinition(type);
            ClassMemberAliases[Classes[type].Key] = Classes[type].MemberAliases;
        }

        public static bool IsDefined(Type type) => Classes.ContainsKey(type);
        public static ClassDefinition GetClassDefinition(Type type)
        {
            var attr = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
            if (attr == null) return null;

            if (!IsDefined(type)) DefineClass(type);
            if (IsDefined(type)) return Classes[type];
            return null;
        }

        public static string CamelCase(string name) =>
            (new string(name[0], 1).ToLower()) + name.Substring(1);

        public static string Generate()
        {
            string generatedDefinitions = String.Join('\n', Classes.Values.Select(c => c.ToString()));
            File.WriteAllTextAsync(DefinitionFilePath, generatedDefinitions);

            string classMemberAliases = JsonConvert.SerializeObject(ClassMemberAliases, Formatting.Indented);
            string classMemberAliasesDeclaration = "export const Aliases: { [className: string]: { [indentifier: string]: string } } =";
            File.WriteAllTextAsync(ClassMemberAliasFilePath, classMemberAliasesDeclaration + classMemberAliases);

            return generatedDefinitions;
        }
    }
}