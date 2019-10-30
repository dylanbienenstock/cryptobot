using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CryptoBot.Scripting.Typings
{
    public static class TypescriptDefinitions {
        public static Dictionary<Type, EnumDefinition> Enums;
        public static Dictionary<Type, ClassDefinition> Classes;
        public static Dictionary<string, Dictionary<string, string>> ClassMemberAliases;

        public static string CompilerDirectory 
        {
            get
            {
                var codebaseFile = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                var outputDirectory = Path.GetDirectoryName(codebaseFile);
                return Path.Join(outputDirectory, "src/Scripting/Compiler");
            }
        }
            
        public static string IncludesDirectory =>
            Path.Join(CompilerDirectory, "include");

        public static string GlobalIncludesFilePath => 
            Path.Join(IncludesDirectory, "global.ts");

        public static string GeneratedDefinitionFilePath => 
            Path.Join(CompilerDirectory, "types.ts");

        public static string ClassMemberAliasFilePath => 
            Path.Join(CompilerDirectory, "alias.ts");

        public static string ConfigFilePath => 
            Path.Join(CompilerDirectory, "tsconfig.json");

        static TypescriptDefinitions()
        {
            Enums = new Dictionary<Type, EnumDefinition>();
            Classes = new Dictionary<Type, ClassDefinition>();
            ClassMemberAliases = new Dictionary<string, Dictionary<string, string>>();

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attr = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
                if (attr == null) continue;
                if (type.IsEnum)  DefineEnum(type);
                if (type.IsClass) DefineClass(type);
            }

            Task.Run(async () =>
            {
                Console.WriteLine("GENERATED TYPEDEFS:");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(await Generate() + "\n");
                Console.ResetColor();
            });
        }

        private static bool CanDefine(Type type, bool nested)
        {
            var defineAttribute = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
            if (defineAttribute == null && !nested) return false;
            if (PrimitiveTypes.IsPrimitive(type))   return false;
            if (!type.IsEnum && !type.IsClass)      return false;
            if (Enums.ContainsKey(type))            return false;
            if (Classes.ContainsKey(type))          return false;
            return true;
        }

        public static void DefineEnum(Type type, bool nested = false)
        {
            if (!CanDefine(type, nested)) return;
            Enums[type] = new EnumDefinition(type);
        }

        public static void DefineClass(Type type, bool nested = false)
        {
            if (!CanDefine(type, nested)) return;
            Classes[type] = new ClassDefinition(type);
            ClassMemberAliases[Classes[type].Key] = Classes[type].MemberAliases;
        }

        public static bool IsDefined(Type type) => Enums.ContainsKey(type) || Classes.ContainsKey(type);
        public static ClassDefinition GetClassDefinition(Type type)
        {
            var attr = (TypescriptDefine)type.GetCustomAttribute(typeof(TypescriptDefine));
            if (attr == null) return null;

            if (!IsDefined(type)) DefineClass(type);
            if (IsDefined(type)) return Classes[type];
            return null;
        }

        public static string CamelCase(string name)
        {
            if (name == "Type") return "$type";
            return name.Substring(0, 1).ToLower() + name.Substring(1).Replace(" ", "_");
        }

        public static string UnCamelCase(string name)
        {
            if (name == "$type") return "Type";
            return name.Substring(0, 1).ToUpper() + name.Substring(1).Replace("_", " ");
        }

        public static async Task<string> GetLibrary()
        {
            string lib = "";
            void IncludeFile(string path) => lib = File.ReadAllText(path) + "\n\n" + lib;

            await Generate();
            IncludeFile(TypescriptDefinitions.GeneratedDefinitionFilePath);
            IncludeFile(TypescriptDefinitions.GlobalIncludesFilePath);

            return lib.Trim();
        }

        public static async Task<string> Generate()
        {
            var enumDefinitions = Enums.Values.Select(e => e.ToString());
            var classDefinitions = Classes.Values.Select(c => c.ToString());
            var allDefinitions = enumDefinitions.Concat(classDefinitions);
            var mergedDefinitions = String
                .Join("\n\n", allDefinitions)
                .Replace('\'', '"')
                .Trim();
            await File.WriteAllTextAsync(GeneratedDefinitionFilePath, mergedDefinitions);

            var classMemberAliases = JsonConvert.SerializeObject(ClassMemberAliases, Formatting.Indented);
            var classMemberAliasesDeclaration = "export const Aliases: { [className: string]: { [indentifier: string]: string } } =";
            await File.WriteAllTextAsync(ClassMemberAliasFilePath, classMemberAliasesDeclaration + classMemberAliases);

            return mergedDefinitions;
        }
    }
}