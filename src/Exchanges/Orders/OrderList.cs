using System;
using System.Collections.Generic;
using System.Diagnostics;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.Exchanges.Orders
{
    public class OrderList
    {
        public OrderSide Side;
        public OrderNode Head;
        public OrderNode Tail;
        public int Count { get; private set; }
        public readonly int Capacity;
        
        
        /// <summary>
        /// Constructs a new OrderListFira
        /// </summary>
        /// <param name="side">
        /// Which side (bidding or asking) this list represents
        /// </param>
        public OrderList(OrderSide side, int capacity)
        {
            Side = side;
            Count = 0;
            Capacity = capacity;
        }

        /// <summary>
        /// This function is a wrapper for the private function _Record
        /// <para>
        /// It performs the following operations
        /// * Inserts, updates, or removes a node using _Record
        /// * Modifies <c>Count</c> according to the output of _Record
        /// </para>
        /// </summary>
        /// <param name="order">
        /// The order to record
        /// </param>
        /// <returns>
        /// The new <c>Count</c> of the list
        /// </returns>
        public int Record(CurrencyOrder order) => (Count += _Record(order));

        public OrderNode[] ToArray()
        {
            var orders = new OrderNode[Count];

            if (Count == 0) return orders;

            orders[0] = Head;

            for (int i = 1; i < Count; i++)
            {
                orders[i] = orders[i - 1].Next;
            }

            return orders;
        }

        /// <summary>
        /// Inserts, updates, or removes an order node, depending on if a node
        /// at the given order's price point exists, and the amount is greater than 0
        /// * Note: This operation does not affect Count
        /// </summary>
        /// <param name="order">
        /// Order to insert or update
        /// </param>
        /// <returns>
        /// Returns: The number of nodes that were insert / removed
        /// </returns>
        private int _Record(CurrencyOrder order)
        {
            // If the order's amount is 0, remove the node with that price
            if (order.Amount == 0) return RemoveNode(order.Price);

            // Create a new node from the order
            var node = new OrderNode(order);

            // If the list is empty, add node as Head & Tail
            if (Count == 0) return InsertInitialNode(node);

            // In order to keep the list sorted, find the correct node to to either
            // A) Update the amount (If prices are equal)
            // B) Insert after (If the found node should come before the new node)
            var seqNode = FindSequentialNode(node);

            // If nothing was found, add the new node before Head
            if (seqNode == null) return InsertNodeBeforeHead(node);

            // If a node with the same price was found, update it
            if (seqNode.Price == order.Price) return UpdateNode(seqNode, order.Amount);

            // If a node that should come before the new 
            // node was found, insert the new node after it
            return InsertNodeAfter(node, seqNode);
        }

        /// <summary>
        /// Inserts the given node as the first to ever be inserted 
        /// into the list, points both Head and Tail at the given node
        /// </summary>
        /// <returns>
        /// Returns 1, this operation adds 1 node
        /// </returns>
        private int InsertInitialNode(OrderNode node)
        {
            Head = node;
            Tail = node;
            return 1;
        }

        /// <summary>
        /// Finds a node that, if the list were sorted, 
        /// would come before the given node
        /// </summary>
        private OrderNode FindSequentialNode(OrderNode beforeNode)
        {
            OrderNode node = Tail;
            bool found = false;

            while (node != null && !found)
            {
                found = Side == OrderSide.Bid
                    ? (node.Price <= beforeNode.Price)
                    : (node.Price >= beforeNode.Price);

                if (found) break;

                node = node.Previous;
            }

            if (!found) return null;

            return node;
        }

        /// <summary>
        /// Inserts the given node at the start of this list, 
        /// and points Head at the given node
        /// </summary>
        /// <returns>
        /// Returns 1, this operation adds 1 node
        /// </returns>
        private int InsertNodeBeforeHead(OrderNode node)
        {
            node.Next = this.Head;
            Head.Previous = node;
            Head = node;
            return 1;
        }

        /// <summary>
        /// Inserts the first given node after the second given node
        /// </summary>
        /// <param name="insertNode">
        /// The node to insert
        /// </param>
        /// <param name="afterNode">
        /// The node before the node to insert
        /// </param>
        /// <returns>
        /// Returns 1, this operation adds 1 node
        /// </returns>
        private int InsertNodeAfter(OrderNode insertNode, OrderNode afterNode)
        {
            if (afterNode == Tail)
            {
                Tail.Next = insertNode;
                insertNode.Previous = Tail;
                Tail = insertNode;
            }
            else
            {
                insertNode.Next = afterNode.Next;
                insertNode.Next.Previous = insertNode;
                afterNode.Next = insertNode;
                insertNode.Previous = afterNode;
            }

            return 1;
        }

        /// <summary>
        /// Updates the given node's amount to the given amount
        /// </summary>
        /// <returns>
        /// Returns 0, this operation doesn't affect the total number of nodes
        /// </returns>
        private static int UpdateNode(OrderNode node, decimal amount)
        {
            node.Amount.Add(amount);
            return 0;
        }

        /// <summary>
        /// Finds and removes a node based on the given price
        /// </summary>
        /// <returns>
        /// Returns -1, this operation removes 1 node
        /// </returns>
        private int RemoveNode(decimal price)
        {
            // If the list is empty, do nothing
            if (Tail == null) return 0;

            // Find the correct node to remove by matching prices
            var node = Tail;
            bool found = false;

            while (!found && node != null)
            {
                if (node.Price == price)
                {
                    found = true;
                    break;
                }

                node = node.Previous;
            }

            // If no node was found with a matching price, do nothing
            if (!found) return 0;

            // Remove the found node
            if (node == Head)
            {
                if (Head.Next != null)
                {
                    Head = Head.Next;
                    Head.Previous = null;
                }
                else
                {
                    Head = null;
                    Tail = null;
                }
            }
            else if (node == Tail)
            {
                Tail = Tail.Previous;
                Tail.Next = null;
            }
            else
            {
                node.Next.Previous = node.Previous;
                node.Previous.Next = node.Next;
            }

            return -1;
        }
    }
}