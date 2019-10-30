using System;

namespace CryptoBot.Scripting.Typings
{
    public class TypescriptDocumentation : Attribute
    {
        public string Comment;

        public TypescriptDocumentation(string comment)
        {
            Comment = comment;
        }

        public override string ToString() => $" * {String.Join("\n * ", Comment.Split('\n'))}";

        public class Parameter : Attribute
        {
            public string Name;
            public string Comment;

            public Parameter(string name, string comment)
            {
                Name = name;
                Comment = comment;
            }

            public override string ToString() => 
                $" * @param {Name} - {String.Join("\n * ", Comment.Split('\n'))}";
        }
    }
}