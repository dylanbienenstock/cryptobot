using System;
using System.IO;

namespace CryptoBot
{
    public static class Journal
    {
        public static string LogFile = Path.Join(".", "journal.log");

        public static void Log(string message = "", bool echo = true)
        {
            if (echo) Console.WriteLine(message);
            File.AppendAllText(Journal.LogFile, message + Environment.NewLine);
        }
    }
}