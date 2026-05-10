using Hazina.API.DocumentStore.Models;
using Hazina.API.DocumentStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.API.DocumentStore.Controllers;

/// <summary>
/// Controller for managing RAG stores
/// </summary>
[ApiController]
[Route("api/v1/rag-stores")]
[Authorize]
public class RAGStoresController : ControllerBase
{
    private readonly RAGStoreManager _storeManager;
    private readonly ILogger<RAGStoresController> _logger;

    public RAGStoresController(
        RAGStoreManager storeManager,
        ILogger<RAGStoresController> logger)
    {
        _storeManager = storeManager;
        _logger = logger;
    }

    /// <summary>
    /// Create a new RAG store
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RAGStore), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RAGStore>> CreateStore(
        [FromBody] CreateRAGStoreRequest request)
    {
        try
        {
            var config = new RAGStoreConfig
            {
                EmbeddingModel = request.EmbeddingModel ?? "text-embedding-3-small",
                LLMModel = request.LLMModel ?? "gpt-4o",
                ChunkingStrategy = request.ChunkingStrategy ?? ChunkingStrategy.Semantic,
                ChunkSize = request.ChunkSize ?? 1000,
                ChunkOverlap = request.ChunkOverlap ?? 200,
                TopK = request.TopK ?? 5,
                MinRelevanceScore = request.MinRelevanceScore ?? 0.7f
            };

            var store = await _storeManager.CreateStoreAsync(
                request.Name,
                request.Description,
                config);

            return CreatedAtAction(
                nameof(GetStore),
                new { id = store.Id },
                store);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RAG store");
            return BadRequest(new { error = "Failed to create RAG store" });
        }
    }

    /// <summary>
    /// Get a RAG store by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RAGStore), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RAGStore>> GetStore(Guid id)
    {
        var store = await _storeManager.GetStoreAsync(id);
        if (store == null)
        {
            return NotFound(new { error = $"RAG store {id} not found" });
        }

        return Ok(store);
    }

    /// <summary>
    /// Get a RAG store by name
    /// </summary>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(RAGStore), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RAGStore>> GetStoreByName(string name)
    {
        var store = await _storeManager.GetStoreByNameAsync(name);
        if (store == null)
        {
            return NotFound(new { error = $"RAG store '{name}' not found" });
        }

        return Ok(store);
    }

    /// <summary>
    /// List all RAG stores
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RAGStore>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RAGStore>>> ListStores()
    {
        var stores = await _storeManager.GetAllStoresAsync();
        return Ok(stores);
    }

    /// <summary>
    /// Update RAG store configuration
    /// </summary>
    [HttpPut("{id}/config")]
    [ProducesResponseType(typeof(RAGStore), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RAGStore>> UpdateConfig(
        Guid id,
        [FromBody] UpdateRAGStoreConfigRequest request)
    {
        try
        {
            var store = await _storeManager.GetStoreAsync(id);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {id} not found" });
            }

            var newConfig = new RAGStoreConfig
            {
                EmbeddingModel = request.EmbeddingModel ?? store.Config.EmbeddingModel,
                LLMModel = request.LLMModel ?? store.Config.LLMModel,
                ChunkingStrategy = request.ChunkingStrategy ?? store.Config.ChunkingStrategy,
                ChunkSize = request.ChunkSize ?? store.Config.ChunkSize,
                ChunkOverlap = request.ChunkOverlap ?? store.Config.ChunkOverlap,
                TopK = request.TopK ?? store.Config.TopK,
                MinRelevanceScore = request.MinRelevanceScore ?? store.Config.MinRelevanceScore
            };

            var updatedStore = await _storeManager.UpdateConfigAsync(id, newConfig);

            return Ok(updatedStore);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RAG store configuration");
            return BadRequest(new { error = "Failed to update configuration" });
        }
    }

    /// <summary>
    /// Delete a RAG store
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteStore(Guid id)
    {
        var success = await _storeManager.DeleteStoreAsync(id);
        if (!success)
        {
            return NotFound(new { error = $"RAG store {id} not found" });
        }

        return NoContent();
    }
}
