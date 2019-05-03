using System;

namespace CryptoBot
{
    public static class DateTimeExtension
    {
        public static double GetQuantizedMilliseconds(this DateTime dateTime, int intervalMilliseconds)
        {
            var milliseconds = (dateTime - DateTime.UnixEpoch).TotalMilliseconds;
            return Math.Floor(milliseconds / intervalMilliseconds) * intervalMilliseconds;
        }

        public static double GetQuantizedMilliseconds(this DateTime dateTime, double intervalMilliseconds) =>
            dateTime.GetQuantizedMilliseconds((int)intervalMilliseconds);

        public static DateTime Quantize(this DateTime dateTime, int intervalMilliseconds) =>
           DateTime.UnixEpoch.AddMilliseconds(dateTime.GetQuantizedMilliseconds(intervalMilliseconds));

        public static DateTime Quantize(this DateTime dateTime, double intervalMilliseconds) =>
           DateTime.UnixEpoch.AddMilliseconds(dateTime.GetQuantizedMilliseconds(intervalMilliseconds));

        public static DateTime FromMilliseconds(double milliseconds) =>
           DateTime.UnixEpoch.AddMilliseconds(milliseconds);
    }
}