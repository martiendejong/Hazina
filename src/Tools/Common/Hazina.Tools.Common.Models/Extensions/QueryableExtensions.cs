using Hazina.Tools.Common.Models.DTO;
using System.Linq.Expressions;

namespace Hazina.Tools.Common.Models.Extensions
{
    /// <summary>
    /// Extension methods for IQueryable to simplify pagination and common query operations
    /// </summary>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Applies pagination to an IQueryable using PagedRequest parameters
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="query">Source query</param>
        /// <param name="request">Pagination request parameters</param>
        /// <returns>Paged response with items and metadata</returns>
        public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
            this IQueryable<T> query,
            PagedRequest request)
        {
            request.Normalize();

            // Get total count before pagination
            var totalCount = query.Count();

            // Apply sorting if specified
            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                query = ApplySorting(query, request.SortBy, request.SortDescending);
            }

            // Apply pagination
            var items = query
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList();

            return new PagedResponse<T>(items, totalCount, request.PageNumber, request.PageSize);
        }

        /// <summary>
        /// Applies pagination to an IQueryable (synchronous version)
        /// </summary>
        public static PagedResponse<T> ToPagedResponse<T>(
            this IQueryable<T> query,
            PagedRequest request)
        {
            request.Normalize();

            var totalCount = query.Count();

            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                query = ApplySorting(query, request.SortBy, request.SortDescending);
            }

            var items = query
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList();

            return new PagedResponse<T>(items, totalCount, request.PageNumber, request.PageSize);
        }

        /// <summary>
        /// Applies dynamic sorting to a query based on property name
        /// </summary>
        private static IQueryable<T> ApplySorting<T>(
            IQueryable<T> query,
            string sortBy,
            bool descending)
        {
            try
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, sortBy);
                var lambda = Expression.Lambda(property, parameter);

                var methodName = descending ? "OrderByDescending" : "OrderBy";
                var resultExpression = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    new[] { typeof(T), property.Type },
                    query.Expression,
                    Expression.Quote(lambda));

                return query.Provider.CreateQuery<T>(resultExpression);
            }
            catch
            {
                // If sorting fails (invalid property name), return unsorted query
                return query;
            }
        }

        /// <summary>
        /// Applies pagination without getting total count (more performant for large datasets)
        /// </summary>
        public static List<T> ToPaged<T>(this IQueryable<T> query, PagedRequest request)
        {
            request.Normalize();

            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                query = ApplySorting(query, request.SortBy, request.SortDescending);
            }

            return query
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList();
        }
    }
}
