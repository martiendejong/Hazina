using Hazina.API.Search.Models;
using Microsoft.EntityFrameworkCore;

namespace Hazina.API.Search.Data;

/// <summary>
/// Repository for managing RAG store metadata persistence
/// </summary>
public class RAGStoreRepository
{
    private readonly SearchDbContext _context;
    private readonly ILogger<RAGStoreRepository> _logger;

    public RAGStoreRepository(
        SearchDbContext context,
        ILogger<RAGStoreRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RAGStore> CreateAsync(RAGStore store)
    {
        store.CreatedAt = DateTime.UtcNow;
        store.UpdatedAt = DateTime.UtcNow;

        _context.RAGStores.Add(store);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created RAG store {StoreId} with name {Name}",
            store.Id, store.Name);

        return store;
    }

    public async Task<RAGStore?> GetByIdAsync(Guid id)
    {
        return await _context.RAGStores
            .Include(s => s.Config)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<RAGStore?> GetByNameAsync(string name)
    {
        return await _context.RAGStores
            .Include(s => s.Config)
            .FirstOrDefaultAsync(s => s.Name == name);
    }

    public async Task<List<RAGStore>> GetAllAsync()
    {
        return await _context.RAGStores
            .Include(s => s.Config)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _context.RAGStores
            .AnyAsync(s => s.Name == name);
    }

    public async Task<RAGStore> UpdateAsync(RAGStore store)
    {
        store.UpdatedAt = DateTime.UtcNow;

        _context.RAGStores.Update(store);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated RAG store {StoreId}", store.Id);

        return store;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var store = await GetByIdAsync(id);
        if (store == null)
        {
            return false;
        }

        _context.RAGStores.Remove(store);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted RAG store {StoreId}", id);

        return true;
    }

    public async Task<int> GetDocumentCountAsync(Guid storeId)
    {
        return await _context.Documents
            .CountAsync(d => d.RAGStoreId == storeId);
    }

    public async Task<long> GetTotalSizeBytesAsync(Guid storeId)
    {
        return await _context.Documents
            .Where(d => d.RAGStoreId == storeId)
            .SumAsync(d => d.SizeBytes);
    }

    public async Task UpdateStatisticsAsync(Guid storeId)
    {
        var store = await GetByIdAsync(storeId);
        if (store == null)
        {
            return;
        }

        store.DocumentCount = await GetDocumentCountAsync(storeId);
        store.TotalSizeBytes = await GetTotalSizeBytesAsync(storeId);
        store.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
