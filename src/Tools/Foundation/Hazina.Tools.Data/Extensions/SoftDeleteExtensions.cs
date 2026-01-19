using Hazina.Tools.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Hazina.Tools.Data.Extensions
{
    /// <summary>
    /// Extension methods for configuring soft delete behavior in Entity Framework Core.
    /// </summary>
    public static class SoftDeleteExtensions
    {
        /// <summary>
        /// Configures global query filters for all entities implementing ISoftDeletable.
        /// Automatically excludes soft-deleted entities from all queries.
        /// </summary>
        /// <param name="modelBuilder">EF Core model builder</param>
        public static void ConfigureSoftDelete(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    // Create expression: entity => !entity.IsDeleted
                    var parameter = Expression.Parameter(entityType.ClrType, "entity");
                    var propertyAccess = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                    var notDeleted = Expression.Not(propertyAccess);
                    var lambda = Expression.Lambda(notDeleted, parameter);

                    // Apply query filter
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }

        /// <summary>
        /// Marks an entity as soft-deleted without removing it from the database.
        /// </summary>
        /// <param name="entity">Entity to soft-delete</param>
        /// <param name="deletedBy">User ID who is deleting the entity (optional)</param>
        public static void SoftDelete(this ISoftDeletable entity, string? deletedBy = null)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = deletedBy;
        }

        /// <summary>
        /// Restores a soft-deleted entity.
        /// </summary>
        /// <param name="entity">Entity to restore</param>
        public static void Restore(this ISoftDeletable entity)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
        }

        /// <summary>
        /// Queries including soft-deleted entities (bypasses the query filter).
        /// Use this when you need to access deleted entities explicitly.
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="dbSet">Database set</param>
        /// <returns>Queryable including deleted entities</returns>
        public static IQueryable<T> IncludeDeleted<T>(this DbSet<T> dbSet) where T : class, ISoftDeletable
        {
            return dbSet.IgnoreQueryFilters();
        }

        /// <summary>
        /// Queries only soft-deleted entities.
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="dbSet">Database set</param>
        /// <returns>Queryable with only deleted entities</returns>
        public static IQueryable<T> OnlyDeleted<T>(this DbSet<T> dbSet) where T : class, ISoftDeletable
        {
            return dbSet.IgnoreQueryFilters().Where(e => e.IsDeleted);
        }
    }
}
