using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jint.Native.Object;

namespace CryptoBot.Scripting
{
    public static class JavascriptHelper
    {
        public static T BindObject<T>(ObjectInstance obj)
        {
            var getFieldsFlags = BindingFlags.Instance | BindingFlags.Public;
            var targetFields = typeof(T).GetFields(getFieldsFlags);
            var fieldBound = targetFields.ToDictionary(f => f.Name, f => false);
            var target = Activator.CreateInstance(typeof(T));

            foreach (var functionDefinition in obj.Prototype.GetOwnProperties())
            {
                if (!functionDefinition.Value.Value.IsObject()) continue;

                var functionName   = functionDefinition.Key;
                var function       = functionDefinition.Value.Value;
                var targetField = targetFields.FirstOrDefault(f => f.Name == functionName);

                if (targetField == null) continue;

                Action action = () => function.Invoke();
                targetField.SetValue(target, action);
                fieldBound[targetField.Name] = true;
            }

            foreach (var targetField in targetFields.Where(f => !fieldBound[f.Name]))
            {
                Action action = () => {};
                targetField.SetValue(target, action);
            }

            return (T)target;
        }
    }
}