using System;
using CryptoBot.Arbitrage;
using CryptoBot.Exchanges;
using Newtonsoft.Json;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.TcpDebug.Json
{
    public class OrderBookConverter : JsonConverter<OrderBook>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, OrderBook orders, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("bids");
            writer.WriteOrderList(orders.Bids);
            writer.WritePropertyName("asks");
            writer.WriteOrderList(orders.Asks);
            writer.WriteEndObject();
        }
        
        public override OrderBook ReadJson(JsonReader reader, Type objectType, OrderBook existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new JsonReaderException("Cannot deserialize OrderBook");
        }
    }

    public static class OrderBookJsonWriter
    {
        public static void WriteOrderList(this JsonWriter writer, OrderList orderList)
        {
            writer.WriteStartArray();
            
            foreach (var node in orderList.ToArray())
                writer.WriteOrderNode(node);

            writer.WriteEndArray();
        }

        public static void WriteOrderNode(this JsonWriter writer, OrderNode node)
        {
            writer.WriteStartArray();
            writer.WriteValue(node.Price);
            writer.WriteStartArray();
            
            foreach (var amount in node.Amount.ToArray())
                writer.WriteValue(amount);

            writer.WriteEndArray();
            writer.WriteEndArray();
        }
    }
}