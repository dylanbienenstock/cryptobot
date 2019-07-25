using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CryptoBot.Scripting.Typings;
using Newtonsoft.Json;

namespace CryptoBot.Indicators
{
    public static class IndicatorList
    {
        private static Dictionary<string, Type> _indicatorTypes;
        private static Dictionary<Type, IndicatorDetails> _indicatorDetails;

        static IndicatorList()
        {
            _indicatorTypes = new Dictionary<string, Type>();
            _indicatorDetails = new Dictionary<Type, IndicatorDetails>();

            Add<Candlestick>();
            Add<RelativeStrengthIndex>();
            Add<MovingAverageConvergenceDivergence>();
            Add<WilliamsFractals>();
        }

        public static void Add<T>() where T : Indicator, new()
        {
            var description = (new T()).Details;
            _indicatorTypes[description.Name] = typeof(T);
            _indicatorDetails[typeof(T)] = description;
        }

        public static Type GetIndicatorType(string indicatorName) =>
            _indicatorTypes[indicatorName];

        public static IndicatorDetails GetIndicatorDescription(string indicatorName) =>
            _indicatorDetails[_indicatorTypes[indicatorName]];

        public static IndicatorDetails[] GetAllIndicatorDescriptions() => 
            _indicatorDetails.Values.ToArray();
    }
}