using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CryptoBot.Scripting.Library;
using CryptoBot.Scripting.Modules;
using CryptoBot.Scripting.Typings;
using Jint;
using Newtonsoft.Json;

namespace CryptoBot.Scripting
{
    public static class JavascriptHost
    {
        public static Subject<InstancedLibraryCall<string>> OnLog;
        public static Subject<InstancedLibraryCall<string>> OnWarn;
        public static Subject<InstancedLibraryCall<string>> OnError;

        static JavascriptHost()
        {
            OnLog = new Subject<InstancedLibraryCall<string>>();
            OnWarn = new Subject<InstancedLibraryCall<string>>();
            OnError = new Subject<InstancedLibraryCall<string>>();

            OnLog.Subscribe(call =>
            {
                Console.WriteLine($"JAVASCRIPT HOST INSTANCE {call.InstanceId} SAYS:");
                Console.WriteLine(call.Arguments);
            });
        }

        public static async Task<string> ExecuteTypescript(string tsSource)
        {
            string synopsis = "";

            try
            {
                var tsCompilerOptions = new TypescriptCompiler.Options()
                {
                    Project = TypescriptDefinitions.ProjectFilePath
                };
                var jsCompiled = await TypescriptCompiler.Compile(tsSource, tsCompilerOptions);
                var jsProcessed = PostProcessJavascript(jsCompiled);
                
                var engine = new Engine();
                Library.JavascriptLibrary.Apply(engine);
                engine.SetValue("log", new Action<object>(Console.WriteLine));
                engine.Execute(jsProcessed);

                var nameRegex = new Regex(@"export(?:\s+)default(?:\s+)class(?:\s+)(.+)(?:\s+){", RegexOptions.ECMAScript);
                var name = nameRegex.Match(tsSource).Groups[1];

                { // Compilation output
                    Console.WriteLine("TYPESCRIPT SOURCE: ");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(tsSource.Trim());
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("COMPILED JAVASCRIPT: ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(jsCompiled.Trim());
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("POST-PROCESSED JAVASCRIPT: ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(jsProcessed.Trim());
                    Console.ResetColor();
                    Console.WriteLine();
                }

                var defaultExportInstance = engine.Execute("new exports.default()")
                    .GetCompletionValue()
                    .AsObject();

                var defaultExportProperties = defaultExportInstance.GetOwnProperties()
                    .Select(kv => $"{kv.Key}: {kv.Value.Value.Type.ToString().ToLower()}");

                var defaultExportMethods = defaultExportInstance.Prototype.GetOwnProperties()
                    .Select(kv => $"{kv.Key}: Function");

                synopsis = $"class {name} { '{' }\n";
                synopsis += String.Join('\n', defaultExportProperties.Select(p => $"    {p};")) + "\n";
                synopsis += String.Join('\n',defaultExportMethods.Select(m => $"    {m};")) + "\n";
                synopsis += "}";

                { // Execution output
                    Console.WriteLine("JAVASCRIPT EXECUTION OUTPUT: ");
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine();
                    Console.WriteLine(synopsis);
                    Console.ResetColor();
                    Console.WriteLine();
                }

                var bound = JavascriptHelper.BindObject<SignalEmitter>(defaultExportInstance);
                bound.OnInit();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Execution failed:");
                Console.WriteLine(ex);
                Console.ResetColor();

                synopsis = ex.Message;
            }

            return synopsis;
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

            var output = String.Join('\n', outputLines);

            if (!output.Contains(header))
                output = header + "\n" + output;

            return output;
        }

        private static string Stringify(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
    }
}