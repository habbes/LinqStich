using System;
using System.Collections.Generic;
using System.Linq;
using LinqStitch;

namespace CliSample
{
    class Program
    {
        static void Main(string[] args)
        {
            var context = new MyDataAggregator();
            var first = context.Customers.Where(x => x.Id == 2).Select(x => new { Id = x.Id, Name = x.Name, Orders = x.Orders }).FirstOrDefault();
            Console.WriteLine($"Name {first.Name} Orders {string.Join(", ", first.Orders.Select(o => o.Id))}");

            var order = context.Orders.Where(o => o.TotalPrice > 1000 && o.Customer.Name == "Cust2").FirstOrDefault();
            Console.WriteLine($"Order {order.Id}, {order.TotalPrice}, {order.Customer?.Name}");
        }
    }

    public static class Data
    {
        public static IQueryable<Customer> GetCustomers()
        {
            return new List<Customer>()
            {
                new(){ Id = 1, Name = "Cust1" },
                new(){ Id = 2, Name = "Cust2" },

            }.AsQueryable();
        }

        public static IQueryable<Order> GetOrders()
        {
            return new List<Order>()
            {
                new(){ Id = 1, CustomerId = 1, TotalPrice = 1000 },
                new(){ Id = 2, CustomerId = 2, TotalPrice = 2000 },
                new(){ Id = 3, CustomerId = 1, TotalPrice = 3000 },
                new(){ Id = 4, CustomerId = 2, TotalPrice = 4000 },
                new(){ Id = 5, CustomerId = 1, TotalPrice = 5000 },
                new(){ Id = 6, CustomerId = 2, TotalPrice = 6000 },

            }.AsQueryable();
        }
    }

    public class MyDataAggregator : DataContext
    {
        public DataSet<Customer> Customers { get; set; }
        public DataSet<Order> Orders { get; set; }

        protected override void OnConfiguring(DataContextBuilder builder)
        {
            builder.DataSet<Customer>()
                .FromQueryable(() => Data.GetCustomers())
                .Property(cust => cust.Orders,
                    orders => orders.OnFetch(context => Data.GetOrders().Where(o => o.CustomerId == context.Element.Id).ToList()));

            builder.DataSet<Order>()
                .FromQueryable(() => Data.GetOrders())
                .Property(order => order.Customer,
                    customer => customer.OnFetch(context => Data.GetCustomers().FirstOrDefault(c => c.Id == context.Element.CustomerId)));
        }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Order> Orders { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }

        public int TotalPrice { get; set; }
        public Customer Customer { get; set; }

        public int CustomerId { get; set; }
    }
}
