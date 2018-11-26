using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Server;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using System.Data.SqlClient;

namespace ContainsOptimization
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (var context = new MyContext())
            {
                context.Database.EnsureCreated();
                var ids = Enumerable.Range(1, 1_000_000).ToList();

                //var query = context.People.Where(p => (ids.Contains(p.Id));

                //var query = context.People.In(ids, p => p.Id).ToList();

                //var query = context.People.FromSql($"select * from People p where p.Id in(select value from string_split({string.Join(",", ids)}, ','))").ToList();


                var query = context.People.FromSql($"select * from People p where p.Id in(select * from {context.CreateTableValuedParameter(ids,"p0")})").ToList();

                Console.WriteLine($"Query returned {query.Count}");
            }
        }
    }

    public static class CollectionPredicateBuilder
    {

        public static IQueryable<TSource> In<TSource, TCollection>(
            this IQueryable<TSource> source,
            IList<TCollection> collection,
            Expression<Func<TSource, TCollection>> selector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            var listType = typeof(List<TCollection>);
            var addMethod = listType.GetMethod("Add");
            var getItemMethod = listType.GetMethod("get_Item");
            var containsMethod = listType.GetMethod("Contains");

            // to-do: if index is > 2100 then we need to use constants
            var initializers = collection
                .Select((value, index) =>
                    Expression.ElementInit(
                        addMethod,
                        new[]
                        {
                            Expression.Call(
                                Expression.Constant(
                                    collection,
                                    listType),
                                getItemMethod,
                                new []
                                {
                                    Expression.Constant(
                                        index,
                                        typeof(int))
                                })
                        }))
                        .ToList();

            var bucket = 1;
            while (initializers.Count > bucket)
            {
                bucket <<= 1;
            }

            bucket = bucket > 2098 ? 2098 : bucket;

            if (initializers.Count > bucket)
            {
                throw new InvalidOperationException("In cannot be used with more than 2100 elements");
            }

            for (var index = initializers.Count; index < bucket; index++)
            {
                initializers.Add(initializers[index - 1]);
            }

            return source.Where(
                Expression.Lambda<Func<TSource, bool>>(
                    Expression.Call(
                        Expression.ListInit(
                            Expression.New(
                                listType),
                            initializers),
                            containsMethod,
                        selector.Body),
                    selector.Parameters));
        }
    }

    public class MyContext : DbContext
    {
        private static readonly ILoggerFactory _loggerFactory = new LoggerFactory()
            .AddConsole((s, l) => l == LogLevel.Debug && s.EndsWith("Command"));

        public DbSet<Person> People { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"data source=(localdb)\mssqllocaldb;database=ContainsOptimization;integrated security=true;connectretrycount=0")
               // .UseLoggerFactory(_loggerFactory)
                .EnableSensitiveDataLogging();
        }
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

}
