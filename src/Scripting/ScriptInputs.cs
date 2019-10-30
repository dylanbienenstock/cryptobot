using System;
using System.Collections.Generic;
using System.Dynamic;
using Newtonsoft.Json;

namespace CryptoBot.Scripting
{
    public class ScriptInputs
    {        
        public static ScriptInputs Empty => new ScriptInputs();

        public bool Complete;
        public List<RangeInput> RequiredRangeInputs;

        private Dictionary<string, object> _inputs;

        public ScriptInputs()
        {
            _inputs             = new Dictionary<string, object>();
            RequiredRangeInputs = new List<RangeInput>();
        }

        public ScriptInputs(Dictionary<string, object> inputs)
        {
            _inputs             = inputs;
            RequiredRangeInputs = new List<RangeInput>();
        }

        public void SetValue(string name, object value)
        {
            _inputs[name] = value;
        }

        public T GetValue<T>(string key, T @default)
        {
            if (!_inputs.ContainsKey(key))
            {
                Complete = false;
                _inputs[key] = @default;
            }
            return (T)_inputs[key];
        }

        public double RequireRangeInput(string key, string label, bool integer, double min, double max, double @default)
        {
            var rangeInput = new RangeInput(key, label, integer, min, max, @default);
            RequiredRangeInputs.Add(rangeInput);
            var value = GetValue<double>(key, @default);
            if (integer) return Math.Round((double)value);
            return value;
        }

        public object ToJsonSchema()
        {
            var properties = new Dictionary<string, dynamic>();
            var required = new List<string>();
            var schema = new { type = "object", properties, required };

            void AddProperty(string key, string type)
            {
                properties[key] = type;
                required.Add(key);
            }

            foreach (var rangeInput in RequiredRangeInputs)
                AddProperty(rangeInput.Key, rangeInput.Integer ? "integer" : "number");

            return schema;
        }

        public struct RangeInput
        {
            public string Key;
            public string Label;
            public bool   Integer;
            public double Minimum;
            public double Default;
            public double Maximum;

            public RangeInput(string key, string label, bool integer, double min, double max, double @default)
            {
                Key     = key;
                Label   = label;
                Integer = integer;
                Minimum = min;
                Maximum = max;
                Default = @default;
            }
        }
    }
}