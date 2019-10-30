// https://stackoverflow.com/questions/14046203/programmatically-compile-typescript-in-c
// Thanks BrunoLM

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting
{
    public static class TypescriptCompiler
    {
        public static string OutputDirectory => 
            Path.Join(Environment.CurrentDirectory, "ts-temp");

        static TypescriptCompiler()
        {
            if (Directory.Exists(OutputDirectory))
                Directory.Delete(OutputDirectory, true);
        }

        public static async Task CreateTsConfig(string sourceFilePath)
        {
            string tsConfig = $@"{'{'}
                `compilerOptions`: {'{'}
                    `plugins`: [{'{'} `transform`: `./transform.ts` {'}'}],
                    `target`: `es5`,
                    `lib`: [`es6`],
                    `module`: `commonjs`,
                    `strict`: false,
                    `esModuleInterop`: true,
                    `experimentalDecorators`: true,
                    `outDir`: `{OutputDirectory}`
                {'}'},
                `files`: [`{sourceFilePath}`]
            {'}'}"
            .Replace('`', '"')
            .Replace("            ", "");

            await File.WriteAllTextAsync(TypescriptDefinitions.ConfigFilePath, tsConfig);
        }

        public static async Task<string> Compile(string tsSource)
        {
            var guid = Guid.NewGuid().ToString();
            var tsPath = Path.Join(OutputDirectory, $"{guid}.ts");
            var jsPath = Path.Join(OutputDirectory, $"{guid}.js");

            if (!Directory.Exists(OutputDirectory))
                Directory.CreateDirectory(OutputDirectory);

            await File.WriteAllTextAsync(tsPath, tsSource);
            await CreateTsConfig(tsPath);

            await Task.Run(() => {
                Process p = new Process();
                ProcessStartInfo psi = new ProcessStartInfo($"ttsc");

                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = TypescriptDefinitions.CompilerDirectory;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                p.StartInfo = psi;
                p.Start();

                var errors = p.StandardError.ReadToEnd();

                p.WaitForExit();

                if (!String.IsNullOrEmpty(errors))
                    throw new InvalidTypeScriptFileException(errors);
            });

            var output = File.ReadAllText(jsPath);
            File.Delete(jsPath);
            File.Delete(tsPath);

            return output;
        }
    }

    public class InvalidTypeScriptFileException : Exception
    {
        public InvalidTypeScriptFileException() : base()
        {

        }
        public InvalidTypeScriptFileException(string message) : base(message)
        {

        }
    }
}