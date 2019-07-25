using System;
using System.Collections.Generic;
using System.Dynamic;

namespace CryptoBot.Scripting.Typings
{
    public static class PrimitiveTypes
    {
        private static Dictionary<Type, string> _primitiveMap = new Dictionary<Type, string>()
        {
            { typeof(Boolean),        "boolean"                },
            { typeof(Int16),          "number"                 },
            { typeof(Int32),          "number"                 },
            { typeof(Int64),          "number"                 },
            { typeof(float),          "number"                 },
            { typeof(double),         "number"                 },
            { typeof(decimal),        "number"                 },
            { typeof(char),           "string"                 },
            { typeof(string),         "string"                 },
            { typeof(object),         "any"                    },
            { typeof(ValueType),      "any"                    },
            { typeof(void),           "void"                   },
            { typeof(ExpandoObject),  "{ [key: string]: any }" },
        };

        public static bool IsPrimitive(Type type)
        {
            return _primitiveMap.ContainsKey(type);
        }

        public static string NameOf(Type type, bool defaultToReflectedName = false)
        {
            _primitiveMap.TryGetValue(type, out string primitiveName);

            if (primitiveName == null) 
                return defaultToReflectedName ? type.Name : "any";

            return primitiveName;
        }
    }
}