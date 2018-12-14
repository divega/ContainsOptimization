using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ContainsOptimization
{
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

}
