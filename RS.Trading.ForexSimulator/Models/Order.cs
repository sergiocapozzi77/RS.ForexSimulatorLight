// <copyright file="Order.cs" company="Racing Solutions Ltd">
// Copyright (c) Racing Solutions Ltd</copyright>

namespace RS.Trading.ForexSimulator.Models
{
    public class Order
    {
        public Order(double open, int orderId, double size, double? sl, double? tp, OrderType type)
        {
            this.Open = open;
            this.OrderId = orderId;
            this.Size = size;
            this.SL = sl;
            this.TP = tp;
            this.Type = type;
        }

        public double Open { get; set; }

        public int OrderId { get; set; }

        public double Size { get; set; }

        public double? SL { get; set; }

        public double? TP { get; set; }

        public OrderType Type { get; set; }
    }
}