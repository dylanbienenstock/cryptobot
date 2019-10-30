using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CryptoBot.Scripting.Typings;
using Jint.Native;
using Jint.Native.Object;

namespace CryptoBot.Scripting
{
    public static class ObjectBinder
    {
        /// <summary>
        /// Creates an instance of T (binding target) and binds the given hosted object to it
        /// </summary>
        /// <param name="context">Context of the script that hosts the given hosted object</param>
        /// <param name="obj">Hosted object to bind</param>
        /// <typeparam name="T">Binding target type</typeparam>
        /// <returns>Bound instance of T</returns>
        public static T BindModule<T>(ScriptContext context, ObjectInstance obj)
        {
            // Get bindable fields, and create an instance of T to bind to
            var getFieldsFlags = BindingFlags.Instance | BindingFlags.Public;
            var targetFields = typeof(T).GetFields(getFieldsFlags);
            var target = Activator.CreateInstance<T>();

            // Keep a record of which actions have been successfully bound
            var fieldBound = targetFields.ToDictionary(f => f.Name, f => false);

            // Iterate through properties that are present in the given script
            foreach (var functionDefinition in obj.Prototype.GetOwnProperties())
            {
                // Filter out null / uninvokable properties
                if (functionDefinition.Value.Value == null) continue;
                if (!functionDefinition.Value.Value.IsObject()) continue;

                // Find cooresponding property on binding target 
                var functionName = functionDefinition.Key;
                var function = functionDefinition.Value.Value;
                var targetField = targetFields.FirstOrDefault(f => f.Name == functionName);

                // Filter out properties that aren't present on binding target 
                if (targetField == null) continue;

                // Create an expression tree that binds hosted object (JS) and binding target (C#) together
                var lambda = CreateBindingExpressionTree(context, obj, function, targetField);
                var action = lambda.Compile();

                // Add binding action to binding target
                targetField.SetValue(target, action);
                fieldBound[targetField.Name] = true;
            }

            // Add dummy actions to the binding target for unbindable methods
            foreach (var targetField in targetFields.Where(f => !fieldBound[f.Name]))
            {
                Action action = () => {};
                targetField.SetValue(target, action);
            }

            // Return the binding target
            return (T)target;
        }

        /// <summary>
        /// Creates an expression tree that can be compiled to an 
        /// action that is bound to the given hosted object.
        /// </summary>
        private static LambdaExpression CreateBindingExpressionTree
        (
            ScriptContext context,
            ObjectInstance obj,
            JsValue function,
            FieldInfo targetField
        )
        {
            // Parameters to be accepted by the result lambda
            List<ParameterExpression> lambdaParameters = targetField.FieldType
                .GetGenericArguments()
                .Select(t => Expression.Parameter(t))
                .ToList();

            // Converts result lambda parameters to an array of objects
            NewArrayExpression lambdaParametersArray = Expression.NewArrayInit
            (
                typeof(object),
                lambdaParameters.Select((_, i) =>
                {
                    return Expression.Convert
                    (
                        expression: lambdaParameters.ElementAt(i),
                        type: typeof(object)
                    );
                })
            );

            // Temporary parameters used by convertExpression
            ParameterExpression selectorObject = Expression.Parameter(typeof(object));
            ParameterExpression selectorIndex = Expression.Parameter(typeof(int));

            // Converts actionParametersArray to an array of JsValues
            MethodCallExpression convertExpression = Expression.Call
            (
                type: typeof(Enumerable),
                methodName: "Select",
                typeArguments: new [] {
                    typeof(object),
                    typeof(JsValue)
                },
                arguments: new Expression[]
                {
                    lambdaParametersArray,
                    Expression.Lambda<Func<object, int, JsValue>>
                    (
                        parameters: new [] { selectorObject, selectorIndex },
                        body: Expression.Call
                        (
                            type: typeof(JsValue),
                            methodName: "FromObject",
                            typeArguments: null,
                            arguments: new Expression[]
                            {
                                Expression.Constant(context.Engine),
                                Expression.ArrayAccess(lambdaParametersArray, selectorIndex)
                            }
                        )
                    )
                }
            );

            // Parameters to be passed to the hosted object
            Expression[] jsInvokeParameters = new Expression[]
            {
                Expression.Constant((JsValue)obj),
                Expression.Call
                (
                    type: typeof(Enumerable),
                    methodName: "ToArray",
                    typeArguments: new []
                    {
                        typeof(JsValue)
                    },
                    arguments: new Expression[]
                    {
                        convertExpression
                    }
                )
            };

            // Method used to invoke bound method on hosted object
            MethodInfo jsInvokeMethod = typeof(JsValue)
                .GetMethod("Invoke", new[] { typeof(JsValue), typeof(JsValue[]) });

            // Invokes bound method on hosted object
            MethodCallExpression jsCall = Expression.Call
            (
                instance: Expression.Constant(function),
                method: jsInvokeMethod,
                arguments: jsInvokeParameters
            );

            // Result lambda (entry point)
            LambdaExpression lambda = Expression.Lambda
            (
                delegateType: targetField.FieldType,
                parameters: lambdaParameters,
                body: jsCall
            );

            return lambda;
        }

        private static T CastRaw<T>(object obj) => (T)obj;

        private static dynamic CastOption(object obj, Type type)
        {
            var methodInfo = typeof(ObjectBinder).GetMethod(nameof(CastRaw), BindingFlags.Static | BindingFlags.NonPublic);
            var genericArguments = new [] { type };
            var genericMethodInfo = methodInfo?.MakeGenericMethod(genericArguments);

            Console.WriteLine($"Casting {obj} to {type.Name}");

            return genericMethodInfo?.Invoke(null, new [] { obj });
        }

        public static T BindOptions<T>(dynamic options)
        {
            T instance = (T)Activator.CreateInstance<T>();
            var optionsDict = (IDictionary<string, object>)options;

            foreach (var field in typeof(T).GetFields())
            {
                try
                {
                    var fieldType = field.FieldType;
                    var optionsKey = TypescriptDefinitions.CamelCase(field.Name);
                    object rawValue = optionsDict[optionsKey];
                    object value = null;

                    if (fieldType.IsEnum)
                        rawValue = CastOption((int)((double)rawValue), fieldType);

                    if (!fieldType.IsAssignableFrom(rawValue.GetType()))
                        throw new Exception($"Invalid option type: [{fieldType}] {field.Name}");

                    value = rawValue;
                    field.SetValue(instance, value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("failed at property: " + field.Name);
                    Console.WriteLine(ex);
                }
            }

            return instance;
        }
    }
}