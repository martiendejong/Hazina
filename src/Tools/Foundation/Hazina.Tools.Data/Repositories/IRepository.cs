using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Hazina.Tools.Data.Repositories
{
    /// <summary>
    /// Generic repository interface for data access operations.
    /// Provides common CRUD operations and query capabilities.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    public interface IRepository<TEntity> where TEntity : class
    {
        // ===== QUERY OPERATIONS =====

        /// <summary>
        /// Gets an entity by its primary key
        /// </summary>
        Task<TEntity?> GetByIdAsync(object id);

        /// <summary>
        /// Gets all entities
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync();

        /// <summary>
        /// Finds entities matching a predicate
        /// </summary>
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Gets the first entity matching a predicate, or null
        /// </summary>
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Checks if any entity matches a predicate
        /// </summary>
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Counts entities matching a predicate
        /// </summary>
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Gets a queryable for advanced scenarios (use with caution)
        /// </summary>
        IQueryable<TEntity> Query();

        // ===== WRITE OPERATIONS =====

        /// <summary>
        /// Adds a new entity
        /// </summary>
        Task<TEntity> AddAsync(TEntity entity);

        /// <summary>
        /// Adds multiple entities
        /// </summary>
        Task AddRangeAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        Task UpdateAsync(TEntity entity);

        /// <summary>
        /// Updates multiple entities
        /// </summary>
        Task UpdateRangeAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// Deletes an entity
        /// </summary>
        Task DeleteAsync(TEntity entity);

        /// <summary>
        /// Deletes multiple entities
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// Deletes entities matching a predicate
        /// </summary>
        Task DeleteWhereAsync(Expression<Func<TEntity, bool>> predicate);

        // ===== UNIT OF WORK =====

        /// <summary>
        /// Saves all changes to the database
        /// Returns the number of affected rows
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
