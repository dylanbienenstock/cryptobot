using System;
using CryptoBot.Scripting.Typings;
using Jint;

namespace CryptoBot.Scripting.Library
{
    public static class JavascriptLibrary
    {
        public static void Apply(Engine engine)
        {
            string instanceId = Guid.NewGuid().ToString();

            Add<JavascriptConsole>(engine, instanceId);
        }

        private static void Add<T>(Engine engine, string instanceId) where T : InstancedLibrary
        {
            var name = typeof(T).Name;
            var definition = TypescriptDefinitions.GetClassDefinition(typeof(T));

            if (definition != null) name = definition.Name;

            var instance = (T)Activator.CreateInstance(typeof(T));
            instance.InstanceId = instanceId;
            engine.SetValue(name, instance);
        }
    }
}