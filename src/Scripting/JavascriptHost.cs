using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CryptoBot.Indicators;
using CryptoBot.Scripting.Library;
using CryptoBot.Scripting.Modules;
using CryptoBot.Scripting.Typings;
using Jint;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Newtonsoft.Json;

namespace CryptoBot.Scripting
{
    public static class JavascriptHost
    {
        public static async Task Execute(ScriptContext context)
        {
            if (context.JavascriptSource == null)
            {
                var tsSource        = context.TypescriptSource;
                var tsPreProcessed  = await PreProcessTypescript(tsSource, context.ModuleType);
                var jsCompiled      = await TypescriptCompiler.Compile(tsPreProcessed);
                var jsPostProcessed = PostProcessJavascript(jsCompiled);;

                context.JavascriptSource = jsPostProcessed;
            }
                
            context.Engine = new Engine();
            Library.JavascriptLibrary.Apply(context.Engine, context);

            try 
            {
                context.Engine.Execute(context.JavascriptSource);
            }
            catch (JavaScriptException ex)
            {
                HandleRuntimeException(context, ex);
            }
        }

        public static void HandleRuntimeException(ScriptContext context, JavaScriptException ex)
        {
            var surroundingLines = 8;
            var lineNumber = ex.LineNumber - 1;
            var lines = context.JavascriptSource.Split("\n");
            var startLineIndex = Math.Max(0, lineNumber - surroundingLines);
            var endLineIndex = Math.Min(lines.Length - 1, lineNumber + surroundingLines);

            for (int i = startLineIndex; i < endLineIndex; i++)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.Write((i + 1) + "  ");

                if (i == lineNumber) 
                    Console.ForegroundColor = ConsoleColor.Red;
                else Console.ResetColor();

                Console.WriteLine(lines[i]);

                if (i == lineNumber)
                {
                    Console.WriteLine(new string(' ', ex.Column + ((i + 1) + "  ").Length) + "^");
                    Console.ResetColor();
                    Console.WriteLine(ex.Message);
                    Console.WriteLine();
                }
            }

            Console.ResetColor();
        }

        private static async Task<string> PreProcessTypescript(string tsSource, ModuleType moduleType)
        {
            var moduleTypeName = Enum.GetName(typeof(ModuleType), moduleType);
            var moduleLibrary  = "__" + TypescriptDefinitions.CamelCase(moduleTypeName);

            var tsPreProcessed = string.Join("\n\n", new string[]
            {
                $"var module = {moduleLibrary};",
                await TypescriptDefinitions.GetLibrary(),
                tsSource
            });
            
            return tsPreProcessed;
        }

        private static string PostProcessJavascript(string jsCompiled)
        {
            var header = "var exports = {};";
            var inputLines = jsCompiled.Split("\n");
            var outputLines = new List<string>();

            for (int i = 0; i < inputLines.Length; i++)
            {
                string line = inputLines[i];

                line = line.Replace("/** @class */ ", "");

                if (line.Contains("use strict") && i == 0)
                {
                    outputLines.AddRange(header.Split("\n"));
                    continue;
                }

                if (line.Contains("__esModule")) continue;

                outputLines.Add(line);
            }

            var jsPostProcessed = String.Join('\n', outputLines);

            if (!jsPostProcessed.Contains(header))
                jsPostProcessed = header + "\n" + jsPostProcessed;

            return jsPostProcessed;
        }

        private static string Stringify(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
    }
}