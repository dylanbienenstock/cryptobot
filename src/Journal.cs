using System;
using System.IO;
using Telegram.Bot.Types.Enums;

namespace CryptoBot
{
    public static class Journal
    {
        public static string LogFile = Path.Join(".", "journal.log");

        public static void Log(string message = "", bool echoConsole = true, bool echoTelegram = false)
        {
            if (echoConsole) Console.WriteLine(message);
            if (echoTelegram) TelegramBot.Send(message, ParseMode.Default);
            
            File.AppendAllText(Journal.LogFile, message + Environment.NewLine);
        }
    }
}