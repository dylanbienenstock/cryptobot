using System;
using System.Globalization;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "console", instanced: true)]
    public class ConsoleLibrary : InstancedLibrary
    {
        public ConsoleLibrary(ScriptContext context) : base(context) { }

        public void Verbose(object text)
        {
            Context.OnVerbose.OnNext(text.ToString());
            Context.OnMessage.OnNext((LogLevel.Verbose, text.ToString()));
        }

        public void Log(object text)
        {
            Context.OnLog.OnNext(text.ToString());
            Context.OnMessage.OnNext((LogLevel.Info, text.ToString()));
        }

        public void Warn(object text)
        {
            Context.OnWarn.OnNext(text.ToString());
            Context.OnMessage.OnNext((LogLevel.Warning, text.ToString()));
        }

        public void Error(object text)
        {
            Context.OnError.OnNext(text.ToString());
            Context.OnMessage.OnNext((LogLevel.Error, text.ToString()));
        }

        public static void WriteMessage(ScriptContext context, (LogLevel Level, string Text) message)
        {
            switch (message.Level)
            {
                case LogLevel.Verbose: Console.ForegroundColor = ConsoleColor.Blue;   break;
                case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case LogLevel.Error:   Console.ForegroundColor = ConsoleColor.Red;    break;
            }

            var shortInstanceId = context.InstanceId.Split('-')[0];
            var logLevelName    = Enum.GetName(typeof(LogLevel), message.Level);
            var timestamp       = DateTime.Now.ToString("t", CultureInfo.GetCultureInfo("en-US"));
            var messageLabel    = $"[{shortInstanceId}, {logLevelName}, {timestamp}]";

            Console.WriteLine($"{messageLabel} {message.Text}");
            Console.ResetColor();
        }
    }
}