using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoBot.Scripting.Typings
{
    public class EnumDefinition
    {
        public string Name;
        public Dictionary<string, int> Items;

        public EnumDefinition(Type type)
        {
            Name = type.Name;
            Items = Enum.GetNames(type)
                .ToDictionary
                (
                    keySelector:     name => name,
                    elementSelector: name => (int)Enum.Parse(type, name)
                );

            Journal.LogColored(ToString(), ConsoleColor.Red);
        }

        public override string ToString()
        {
            var open = $"enum {Name} {'{'}\n    ";
            var content = String.Join
            (
                "\n    ", 
                Items.Select((item, i) => $"{item.Key} = {item.Value}{(i < Items.Count - 1 ? "," : "")}")
            );
            var close = "\n}";

            return open + content + close;
        }
    }
}