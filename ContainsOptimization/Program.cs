using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ContainsOptimization
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (var context = new MyContext())
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                var ids = Enumerable.Range(1, 2_098).ToList();
                
                Test("standard Contains", context.People.Where(p => (ids.Contains(p.Id))));
                Test("Parameter rewrite", context.People.In(ids, p => p.Id));
                Test("Split function", context.People.FromSql($"select * from People p where p.Id in(select value from string_split({string.Join(",", ids)}, ','))"));
                Test("table-valued parameter", context.People.FromSql($"select * from People p where p.Id in(select * from {context.CreateTableValuedParameter(ids, "p0")})"));
            }
        }

        private static void Test(string method, IQueryable<Person> query)
        {
            var sw = new Stopwatch();
            sw.Start();
            Console.WriteLine($"Query returned {query.ToList().Count} rows in {sw.ElapsedMilliseconds} milliseconds using {method}.");
        }
    }

    public class MyContext : DbContext
    {
        public DbSet<Person> People { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"data source=(localdb)\mssqllocaldb;database=ContainsOptimization;integrated security=true;connectretrycount=0");
        }
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

}
