// https://stackoverflow.com/questions/14046203/programmatically-compile-typescript-in-c
// Thanks BrunoLM

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting
{
    public static class TypescriptCompiler
    {
        // helper class to add parameters to the compiler
        public class Options
        {
            private static Options @default;
            public static Options Default
            {
                get
                {
                    if (@default == null)
                        @default = new Options();

                    return @default;
                }
            }

            public enum Version
            {
                ES5,
                ES3,
            }

            public bool EmitComments { get; set; }
            public bool GenerateDeclaration { get; set; }
            public bool GenerateSourceMaps { get; set; }
            public string OutPath { get; set; }
            public string Project { get; set; }
            public Version TargetVersion { get; set; }

            public Options() { }

            public Options(bool emitComments = false
                , bool generateDeclaration = false
                , bool generateSourceMaps = false
                , string outPath = null
                , Version targetVersion = Version.ES5)
            {
                EmitComments = emitComments;
                GenerateDeclaration = generateDeclaration;
                GenerateSourceMaps = generateSourceMaps;
                OutPath = outPath;
                TargetVersion = targetVersion;
            }
        }

        public static async Task CreateTsConfig(string sourceFilePath, string configFilePath)
        {
            string tsConfig = $@"{'{'}
                `compilerOptions`: {'{'}
                    `plugins`: [{'{'} `transform`: `./transform.ts` {'}'}],
                    `target`: `es5`,
                    `module`: `commonjs`,
                    `strict`: true,
                    `esModuleInterop`: true,
                {'}'},
                `files`: [`{sourceFilePath}`]
            {'}'}"
            .Replace('`', '"')
            .Replace("            ", "");

            await File.WriteAllTextAsync(configFilePath, tsConfig);
        }

        public static async Task<string> Compile(string tsSource, Options options = null)
        {
            var guid = Guid.NewGuid().ToString();
            var tsDir = Path.Join(Environment.CurrentDirectory, "ts-temp");
            var tsPath = Path.Join(tsDir, $"{guid}.ts");
            var jsPath = Path.Join(tsDir, $"{guid}.js");

            if (!Directory.Exists(tsDir))
                Directory.CreateDirectory(tsDir);

            await File.WriteAllTextAsync(tsPath, TypescriptDefinitions.Generate() + "\n\n");
            await File.AppendAllTextAsync(tsPath, tsSource);
            await CreateTsConfig(tsPath, options.Project);

            await Task.Run(() => {
                if (options == null)
                    options = Options.Default;

                var d = new Dictionary<string, string>();

                if (options.Project != null)
                    d.Add("-p", options.Project);

                if (options.EmitComments)
                    d.Add("-c", null);

                if (options.GenerateDeclaration)
                    d.Add("-d", null);

                if (options.GenerateSourceMaps)
                    d.Add("--sourcemap", null);

                if (!String.IsNullOrEmpty(options.OutPath))
                    d.Add("--out", options.OutPath);

                d.Add("--target", options.TargetVersion.ToString());

                // this will invoke `tsc` passing the TS path and other
                // parameters defined in Options parameter
                Process p = new Process();
                ProcessStartInfo psi = new ProcessStartInfo("ttsc");

                // run without showing console windows
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = TypescriptDefinitions.DefinitionFileDirectory;

                // redirects the compiler error output, so we can read
                // and display errors if any
                psi.RedirectStandardError = true;

                p.StartInfo = psi;

                p.Start();

                // reads the error output
                var msg = p.StandardError.ReadToEnd();

                // make sure it finished executing before proceeding 
                p.WaitForExit();

                // if there were errors, throw an exception
                if (!String.IsNullOrEmpty(msg))
                    throw new InvalidTypeScriptFileException(msg);
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