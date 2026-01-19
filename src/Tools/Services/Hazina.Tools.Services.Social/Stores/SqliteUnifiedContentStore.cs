using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;
using Hazina.Tools.Services.Social.Models;

namespace Hazina.Tools.Services.Social.Stores;

/// <summary>
/// SQLite-based unified content storage for ALL platforms.
/// Provides structured storage with full-text search for WordPress, LinkedIn, Instagram, etc.
/// </summary>
public class SqliteUnifiedContentStore : IUnifiedContentStore
{
    private readonly ILogger<SqliteUnifiedContentStore> _logger;
    private readonly Func<string, string> _databasePathResolver;

    public SqliteUnifiedContentStore(
        ILogger<SqliteUnifiedContentStore> logger,
        Func<string, string> databasePathResolver)
    {
        _logger = logger;
        _databasePathResolver = databasePathResolver;
    }

    private async Task<SqliteConnection> GetConnectionAsync(string projectId, CancellationToken cancellationToken)
    {
        var dbPath = _databasePathResolver(projectId);
        var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            -- Main unified content table
            CREATE TABLE IF NOT EXISTS unified_content (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL,
                account_id TEXT NOT NULL,
                source_type TEXT NOT NULL,
                source_id TEXT NOT NULL,
                source_url TEXT,
                content_type TEXT NOT NULL,
                title TEXT,
                content TEXT NOT NULL,
                summary TEXT,
                content_html TEXT,
                content_plaintext TEXT,
                featured_image_url TEXT,
                author_name TEXT,
                author_id TEXT,
                published_at TEXT NOT NULL,
                updated_at TEXT,
                imported_at TEXT NOT NULL,
                last_synced_at TEXT,
                like_count INTEGER DEFAULT 0,
                comment_count INTEGER DEFAULT 0,
                share_count INTEGER DEFAULT 0,
                view_count INTEGER DEFAULT 0,
                engagement_rate REAL DEFAULT 0.0,
                tags TEXT,
                categories TEXT,
                language TEXT,
                detected_topics TEXT,
                sentiment TEXT,
                tone TEXT,
                status TEXT DEFAULT 'published',
                is_historical INTEGER DEFAULT 1,
                display_on_calendar INTEGER DEFAULT 1,
                is_deleted INTEGER DEFAULT 0,
                document_store_id TEXT,
                embedding_id TEXT,
                platform_metadata TEXT,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_unified_project ON unified_content(project_id);
            CREATE INDEX IF NOT EXISTS idx_unified_account ON unified_content(account_id);
            CREATE INDEX IF NOT EXISTS idx_unified_source ON unified_content(source_type, source_id);
            CREATE INDEX IF NOT EXISTS idx_unified_published ON unified_content(published_at);
            CREATE INDEX IF NOT EXISTS idx_unified_calendar ON unified_content(project_id, published_at, display_on_calendar);
            CREATE INDEX IF NOT EXISTS idx_unified_content_type ON unified_content(project_id, content_type);

            -- Media table
            CREATE TABLE IF NOT EXISTS content_media (
                id TEXT PRIMARY KEY,
                content_id TEXT NOT NULL,
                media_type TEXT NOT NULL,
                url TEXT NOT NULL,
                local_url TEXT,
                thumbnail_url TEXT,
                width INTEGER,
                height INTEGER,
                duration_seconds INTEGER,
                file_size_bytes INTEGER,
                mime_type TEXT,
                display_order INTEGER DEFAULT 0,
                is_featured INTEGER DEFAULT 0,
                caption TEXT,
                alt_text TEXT,
                imported_at TEXT NOT NULL,
                FOREIGN KEY (content_id) REFERENCES unified_content(id)
            );

            CREATE INDEX IF NOT EXISTS idx_media_content ON content_media(content_id);

            -- Comments table
            CREATE TABLE IF NOT EXISTS content_comments (
                id TEXT PRIMARY KEY,
                content_id TEXT NOT NULL,
                parent_comment_id TEXT,
                source_type TEXT NOT NULL,
                source_comment_id TEXT NOT NULL,
                author_name TEXT NOT NULL,
                author_id TEXT,
                author_avatar_url TEXT,
                comment_text TEXT NOT NULL,
                comment_html TEXT,
                like_count INTEGER DEFAULT 0,
                reply_count INTEGER DEFAULT 0,
                posted_at TEXT NOT NULL,
                imported_at TEXT NOT NULL,
                is_deleted INTEGER DEFAULT 0,
                sentiment TEXT,
                is_question INTEGER DEFAULT 0,
                requires_response INTEGER DEFAULT 0,
                FOREIGN KEY (content_id) REFERENCES unified_content(id)
            );

            CREATE INDEX IF NOT EXISTS idx_comments_content ON content_comments(content_id);

            -- Writing profiles table
            CREATE TABLE IF NOT EXISTS writing_profiles (
                project_id TEXT PRIMARY KEY,
                dominant_tone TEXT,
                avg_content_length INTEGER,
                avg_sentence_length INTEGER,
                vocabulary_level TEXT,
                top_topics TEXT,
                brand_keywords TEXT,
                common_phrases TEXT,
                style_summary TEXT,
                sample_count INTEGER,
                created_at TEXT NOT NULL,
                last_updated_at TEXT NOT NULL
            );

            -- Full-text search
            CREATE VIRTUAL TABLE IF NOT EXISTS unified_content_fts USING fts5(
                id,
                title,
                content,
                tags,
                content='unified_content',
                content_rowid='rowid'
            );
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveContentAsync(
        string projectId,
        IEnumerable<UnifiedContent> content,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var item in content)
            {
                await SaveContentItemAsync(connection, item, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Saved {Count} unified content items to project {ProjectId}",
                content.Count(), projectId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to save unified content for project {ProjectId}", projectId);
            throw;
        }
    }

    private async Task SaveContentItemAsync(
        SqliteConnection connection,
        UnifiedContent content,
        CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT OR REPLACE INTO unified_content (
                id, project_id, account_id, source_type, source_id, source_url,
                content_type, title, content, summary, content_html, content_plaintext,
                featured_image_url, author_name, author_id,
                published_at, updated_at, imported_at, last_synced_at,
                like_count, comment_count, share_count, view_count, engagement_rate,
                tags, categories, language,
                detected_topics, sentiment, tone,
                status, is_historical, display_on_calendar, is_deleted,
                document_store_id, embedding_id,
                platform_metadata, created_at
            ) VALUES (
                @Id, @ProjectId, @AccountId, @SourceType, @SourceId, @SourceUrl,
                @ContentType, @Title, @Content, @Summary, @ContentHtml, @ContentPlainText,
                @FeaturedImageUrl, @AuthorName, @AuthorId,
                @PublishedAt, @UpdatedAt, @ImportedAt, @LastSyncedAt,
                @LikeCount, @CommentCount, @ShareCount, @ViewCount, @EngagementRate,
                @Tags, @Categories, @Language,
                @DetectedTopics, @Sentiment, @Tone,
                @Status, @IsHistorical, @DisplayOnCalendar, @IsDeleted,
                @DocumentStoreId, @EmbeddingId,
                @PlatformMetadata, @CreatedAt
            )";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@Id", content.Id);
        command.Parameters.AddWithValue("@ProjectId", content.ProjectId);
        command.Parameters.AddWithValue("@AccountId", content.AccountId);
        command.Parameters.AddWithValue("@SourceType", content.SourceType);
        command.Parameters.AddWithValue("@SourceId", content.SourceId);
        command.Parameters.AddWithValue("@SourceUrl", content.SourceUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentType", content.ContentType);
        command.Parameters.AddWithValue("@Title", content.Title ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Content", content.Content);
        command.Parameters.AddWithValue("@Summary", content.Summary ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentHtml", content.ContentHtml ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentPlainText", content.ContentPlainText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FeaturedImageUrl", content.FeaturedImageUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AuthorName", content.AuthorName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AuthorId", content.AuthorId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PublishedAt", content.PublishedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", content.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ImportedAt", content.ImportedAt.ToString("O"));
        command.Parameters.AddWithValue("@LastSyncedAt", content.LastSyncedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LikeCount", content.LikeCount);
        command.Parameters.AddWithValue("@CommentCount", content.CommentCount);
        command.Parameters.AddWithValue("@ShareCount", content.ShareCount);
        command.Parameters.AddWithValue("@ViewCount", content.ViewCount);
        command.Parameters.AddWithValue("@EngagementRate", content.EngagementRate);
        command.Parameters.AddWithValue("@Tags", JsonSerializer.Serialize(content.Tags));
        command.Parameters.AddWithValue("@Categories", JsonSerializer.Serialize(content.Categories));
        command.Parameters.AddWithValue("@Language", content.Language ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DetectedTopics", JsonSerializer.Serialize(content.DetectedTopics));
        command.Parameters.AddWithValue("@Sentiment", content.Sentiment ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Tone", content.Tone ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", content.Status);
        command.Parameters.AddWithValue("@IsHistorical", content.IsHistorical ? 1 : 0);
        command.Parameters.AddWithValue("@DisplayOnCalendar", content.DisplayOnCalendar ? 1 : 0);
        command.Parameters.AddWithValue("@IsDeleted", content.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("@DocumentStoreId", content.DocumentStoreId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EmbeddingId", content.EmbeddingId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PlatformMetadata", JsonSerializer.Serialize(content.PlatformMetadata));
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);

        // Save media
        foreach (var media in content.Media)
        {
            await SaveMediaAsync(connection, media, cancellationToken);
        }

        // Save comments
        foreach (var comment in content.Comments)
        {
            await SaveCommentAsync(connection, comment, cancellationToken);
        }
    }

    private async Task SaveMediaAsync(
        SqliteConnection connection,
        ContentMedia media,
        CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT OR REPLACE INTO content_media (
                id, content_id, media_type, url, local_url, thumbnail_url,
                width, height, duration_seconds, file_size_bytes, mime_type,
                display_order, is_featured, caption, alt_text, imported_at
            ) VALUES (
                @Id, @ContentId, @MediaType, @Url, @LocalUrl, @ThumbnailUrl,
                @Width, @Height, @DurationSeconds, @FileSizeBytes, @MimeType,
                @DisplayOrder, @IsFeatured, @Caption, @AltText, @ImportedAt
            )";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@Id", media.Id);
        command.Parameters.AddWithValue("@ContentId", media.ContentId);
        command.Parameters.AddWithValue("@MediaType", media.Type.ToString());
        command.Parameters.AddWithValue("@Url", media.Url);
        command.Parameters.AddWithValue("@LocalUrl", media.LocalUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ThumbnailUrl", media.ThumbnailUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Width", media.Width ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Height", media.Height ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DurationSeconds", media.DurationSeconds ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FileSizeBytes", media.FileSizeBytes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@MimeType", media.MimeType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DisplayOrder", media.DisplayOrder);
        command.Parameters.AddWithValue("@IsFeatured", media.IsFeatured ? 1 : 0);
        command.Parameters.AddWithValue("@Caption", media.Caption ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AltText", media.AltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ImportedAt", media.ImportedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveCommentAsync(
        SqliteConnection connection,
        ContentComment comment,
        CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT OR REPLACE INTO content_comments (
                id, content_id, parent_comment_id, source_type, source_comment_id,
                author_name, author_id, author_avatar_url,
                comment_text, comment_html,
                like_count, reply_count,
                posted_at, imported_at, is_deleted,
                sentiment, is_question, requires_response
            ) VALUES (
                @Id, @ContentId, @ParentCommentId, @SourceType, @SourceCommentId,
                @AuthorName, @AuthorId, @AuthorAvatarUrl,
                @CommentText, @CommentHtml,
                @LikeCount, @ReplyCount,
                @PostedAt, @ImportedAt, @IsDeleted,
                @Sentiment, @IsQuestion, @RequiresResponse
            )";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@Id", comment.Id);
        command.Parameters.AddWithValue("@ContentId", comment.ContentId);
        command.Parameters.AddWithValue("@ParentCommentId", comment.ParentCommentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceType", comment.SourceType);
        command.Parameters.AddWithValue("@SourceCommentId", comment.SourceCommentId);
        command.Parameters.AddWithValue("@AuthorName", comment.AuthorName);
        command.Parameters.AddWithValue("@AuthorId", comment.AuthorId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AuthorAvatarUrl", comment.AuthorAvatarUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CommentText", comment.CommentText);
        command.Parameters.AddWithValue("@CommentHtml", comment.CommentHtml ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LikeCount", comment.LikeCount);
        command.Parameters.AddWithValue("@ReplyCount", comment.ReplyCount);
        command.Parameters.AddWithValue("@PostedAt", comment.PostedAt.ToString("O"));
        command.Parameters.AddWithValue("@ImportedAt", comment.ImportedAt.ToString("O"));
        command.Parameters.AddWithValue("@IsDeleted", comment.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("@Sentiment", comment.Sentiment ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsQuestion", comment.IsQuestion ? 1 : 0);
        command.Parameters.AddWithValue("@RequiresResponse", comment.RequiresResponse ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<UnifiedContent?> GetContentAsync(
        string projectId,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = "SELECT * FROM unified_content WHERE id = @Id AND project_id = @ProjectId AND is_deleted = 0";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Id", contentId);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var content = await MapToUnifiedContentAsync(reader, connection, cancellationToken);
            return content;
        }

        return null;
    }

    public async Task<List<UnifiedContent>> GetContentListAsync(
        string projectId,
        UnifiedContentQuery query,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var whereClauses = new List<string> { "project_id = @ProjectId", "is_deleted = 0" };
        var parameters = new Dictionary<string, object> { { "@ProjectId", projectId } };

        if (!string.IsNullOrEmpty(query.AccountId))
        {
            whereClauses.Add("account_id = @AccountId");
            parameters["@AccountId"] = query.AccountId;
        }

        if (query.SourceTypes?.Any() == true)
        {
            whereClauses.Add($"source_type IN ({string.Join(",", query.SourceTypes.Select((_, i) => $"@SourceType{i}"))})");
            for (int i = 0; i < query.SourceTypes.Count; i++)
            {
                parameters[$"@SourceType{i}"] = query.SourceTypes[i];
            }
        }

        if (query.ContentTypes?.Any() == true)
        {
            whereClauses.Add($"content_type IN ({string.Join(",", query.ContentTypes.Select((_, i) => $"@ContentType{i}"))})");
            for (int i = 0; i < query.ContentTypes.Count; i++)
            {
                parameters[$"@ContentType{i}"] = query.ContentTypes[i];
            }
        }

        if (query.Since.HasValue)
        {
            whereClauses.Add("published_at >= @Since");
            parameters["@Since"] = query.Since.Value.ToString("O");
        }

        if (query.Until.HasValue)
        {
            whereClauses.Add("published_at <= @Until");
            parameters["@Until"] = query.Until.Value.ToString("O");
        }

        var orderDirection = query.Descending ? "DESC" : "ASC";
        var orderBy = query.OrderBy ?? "published_at";

        var sql = $@"
            SELECT * FROM unified_content
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY {orderBy} {orderDirection}
            LIMIT @Limit OFFSET @Offset";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var param in parameters)
        {
            command.Parameters.AddWithValue(param.Key, param.Value);
        }
        command.Parameters.AddWithValue("@Limit", query.Limit);
        command.Parameters.AddWithValue("@Offset", query.Offset);

        var results = new List<UnifiedContent>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var content = await MapToUnifiedContentAsync(reader, connection, cancellationToken);
            results.Add(content);
        }

        return results;
    }

    public async Task DeleteAccountContentAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = "UPDATE unified_content SET is_deleted = 1 WHERE project_id = @ProjectId AND account_id = @AccountId";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Soft-deleted content for account {AccountId} in project {ProjectId}",
            accountId, projectId);
    }

    public async Task<List<UnifiedContent>> SearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = @"
            SELECT c.* FROM unified_content c
            JOIN unified_content_fts fts ON c.id = fts.id
            WHERE fts MATCH @Query
            AND c.project_id = @ProjectId
            AND c.is_deleted = 0
            LIMIT @Limit";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Query", query);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@Limit", options.Limit);

        var results = new List<UnifiedContent>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var content = await MapToUnifiedContentAsync(reader, connection, cancellationToken);
            results.Add(content);
        }

        return results;
    }

    public async Task<List<UnifiedContent>> SemanticSearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        // Placeholder for semantic search using embeddings
        // This would integrate with vector database or embedding service
        _logger.LogWarning("Semantic search not yet implemented, falling back to full-text search");
        return await SearchAsync(projectId, query, options, cancellationToken);
    }

    public async Task<List<UnifiedContent>> GetCalendarEventsAsync(
        string projectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = @"
            SELECT * FROM unified_content
            WHERE project_id = @ProjectId
            AND published_at >= @StartDate
            AND published_at <= @EndDate
            AND display_on_calendar = 1
            AND is_deleted = 0
            ORDER BY published_at ASC";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@StartDate", startDate.ToString("O"));
        command.Parameters.AddWithValue("@EndDate", endDate.ToString("O"));

        var results = new List<UnifiedContent>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var content = await MapToUnifiedContentAsync(reader, connection, cancellationToken);
            results.Add(content);
        }

        return results;
    }

    public async Task<UnifiedContentStats> GetStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = @"
            SELECT
                COUNT(*) as total_items,
                SUM(comment_count) as total_comments,
                SUM(like_count) as total_likes,
                SUM(share_count) as total_shares,
                SUM(view_count) as total_views,
                MIN(published_at) as oldest_content,
                MAX(published_at) as newest_content
            FROM unified_content
            WHERE project_id = @ProjectId AND is_deleted = 0";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ProjectId", projectId);

        var stats = new UnifiedContentStats();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            stats.TotalItems = reader.GetInt32(0);
            stats.TotalComments = reader.GetInt32(1);
            stats.TotalLikes = reader.GetInt64(2);
            stats.TotalShares = reader.GetInt64(3);
            stats.TotalViews = reader.GetInt64(4);

            if (!reader.IsDBNull(5))
                stats.OldestContent = DateTime.Parse(reader.GetString(5));
            if (!reader.IsDBNull(6))
                stats.NewestContent = DateTime.Parse(reader.GetString(6));
        }

        // Get counts by source
        var sourceSQL = @"
            SELECT source_type, COUNT(*)
            FROM unified_content
            WHERE project_id = @ProjectId AND is_deleted = 0
            GROUP BY source_type";

        using var sourceCmd = connection.CreateCommand();
        sourceCmd.CommandText = sourceSQL;
        sourceCmd.Parameters.AddWithValue("@ProjectId", projectId);

        using var sourceReader = await sourceCmd.ExecuteReaderAsync(cancellationToken);
        while (await sourceReader.ReadAsync(cancellationToken))
        {
            stats.ItemsBySource[sourceReader.GetString(0)] = sourceReader.GetInt32(1);
        }

        // Get counts by type
        var typeSQL = @"
            SELECT content_type, COUNT(*)
            FROM unified_content
            WHERE project_id = @ProjectId AND is_deleted = 0
            GROUP BY content_type";

        using var typeCmd = connection.CreateCommand();
        typeCmd.CommandText = typeSQL;
        typeCmd.Parameters.AddWithValue("@ProjectId", projectId);

        using var typeReader = await typeCmd.ExecuteReaderAsync(cancellationToken);
        while (await typeReader.ReadAsync(cancellationToken))
        {
            stats.ItemsByType[typeReader.GetString(0)] = typeReader.GetInt32(1);
        }

        return stats;
    }

    public async Task<List<UnifiedContent>> GetTopPerformingAsync(
        string projectId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = @"
            SELECT * FROM unified_content
            WHERE project_id = @ProjectId AND is_deleted = 0
            ORDER BY engagement_rate DESC
            LIMIT @Limit";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@Limit", limit);

        var results = new List<UnifiedContent>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var content = await MapToUnifiedContentAsync(reader, connection, cancellationToken);
            results.Add(content);
        }

        return results;
    }

    private async Task<UnifiedContent> MapToUnifiedContentAsync(
        SqliteDataReader reader,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var content = new UnifiedContent
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            ProjectId = reader.GetString(reader.GetOrdinal("project_id")),
            AccountId = reader.GetString(reader.GetOrdinal("account_id")),
            SourceType = reader.GetString(reader.GetOrdinal("source_type")),
            SourceId = reader.GetString(reader.GetOrdinal("source_id")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("source_url")) ? null : reader.GetString(reader.GetOrdinal("source_url")),
            ContentType = reader.GetString(reader.GetOrdinal("content_type")),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            Summary = reader.IsDBNull(reader.GetOrdinal("summary")) ? null : reader.GetString(reader.GetOrdinal("summary")),
            ContentHtml = reader.IsDBNull(reader.GetOrdinal("content_html")) ? null : reader.GetString(reader.GetOrdinal("content_html")),
            ContentPlainText = reader.IsDBNull(reader.GetOrdinal("content_plaintext")) ? null : reader.GetString(reader.GetOrdinal("content_plaintext")),
            FeaturedImageUrl = reader.IsDBNull(reader.GetOrdinal("featured_image_url")) ? null : reader.GetString(reader.GetOrdinal("featured_image_url")),
            AuthorName = reader.IsDBNull(reader.GetOrdinal("author_name")) ? null : reader.GetString(reader.GetOrdinal("author_name")),
            AuthorId = reader.IsDBNull(reader.GetOrdinal("author_id")) ? null : reader.GetString(reader.GetOrdinal("author_id")),
            PublishedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("published_at"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            ImportedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("imported_at"))),
            LastSyncedAt = reader.IsDBNull(reader.GetOrdinal("last_synced_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_synced_at"))),
            LikeCount = reader.GetInt32(reader.GetOrdinal("like_count")),
            CommentCount = reader.GetInt32(reader.GetOrdinal("comment_count")),
            ShareCount = reader.GetInt32(reader.GetOrdinal("share_count")),
            ViewCount = reader.GetInt32(reader.GetOrdinal("view_count")),
            EngagementRate = reader.GetDouble(reader.GetOrdinal("engagement_rate")),
            Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("tags"))) ?? new List<string>(),
            Categories = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("categories"))) ?? new List<string>(),
            Language = reader.IsDBNull(reader.GetOrdinal("language")) ? null : reader.GetString(reader.GetOrdinal("language")),
            DetectedTopics = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("detected_topics"))) ?? new List<string>(),
            Sentiment = reader.IsDBNull(reader.GetOrdinal("sentiment")) ? null : reader.GetString(reader.GetOrdinal("sentiment")),
            Tone = reader.IsDBNull(reader.GetOrdinal("tone")) ? null : reader.GetString(reader.GetOrdinal("tone")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            IsHistorical = reader.GetInt32(reader.GetOrdinal("is_historical")) == 1,
            DisplayOnCalendar = reader.GetInt32(reader.GetOrdinal("display_on_calendar")) == 1,
            IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
            DocumentStoreId = reader.IsDBNull(reader.GetOrdinal("document_store_id")) ? null : reader.GetString(reader.GetOrdinal("document_store_id")),
            EmbeddingId = reader.IsDBNull(reader.GetOrdinal("embedding_id")) ? null : reader.GetString(reader.GetOrdinal("embedding_id")),
            PlatformMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(reader.GetOrdinal("platform_metadata"))) ?? new Dictionary<string, object>()
        };

        // Load media
        content.Media = await LoadMediaAsync(connection, content.Id, cancellationToken);

        // Load comments
        content.Comments = await LoadCommentsAsync(connection, content.Id, cancellationToken);

        return content;
    }

    private async Task<List<ContentMedia>> LoadMediaAsync(
        SqliteConnection connection,
        string contentId,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM content_media WHERE content_id = @ContentId ORDER BY display_order";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ContentId", contentId);

        var media = new List<ContentMedia>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            media.Add(new ContentMedia
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                ContentId = reader.GetString(reader.GetOrdinal("content_id")),
                Type = Enum.Parse<MediaType>(reader.GetString(reader.GetOrdinal("media_type"))),
                Url = reader.GetString(reader.GetOrdinal("url")),
                LocalUrl = reader.IsDBNull(reader.GetOrdinal("local_url")) ? null : reader.GetString(reader.GetOrdinal("local_url")),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url")) ? null : reader.GetString(reader.GetOrdinal("thumbnail_url")),
                Width = reader.IsDBNull(reader.GetOrdinal("width")) ? null : reader.GetInt32(reader.GetOrdinal("width")),
                Height = reader.IsDBNull(reader.GetOrdinal("height")) ? null : reader.GetInt32(reader.GetOrdinal("height")),
                DurationSeconds = reader.IsDBNull(reader.GetOrdinal("duration_seconds")) ? null : reader.GetInt32(reader.GetOrdinal("duration_seconds")),
                FileSizeBytes = reader.IsDBNull(reader.GetOrdinal("file_size_bytes")) ? null : reader.GetInt64(reader.GetOrdinal("file_size_bytes")),
                MimeType = reader.IsDBNull(reader.GetOrdinal("mime_type")) ? null : reader.GetString(reader.GetOrdinal("mime_type")),
                DisplayOrder = reader.GetInt32(reader.GetOrdinal("display_order")),
                IsFeatured = reader.GetInt32(reader.GetOrdinal("is_featured")) == 1,
                Caption = reader.IsDBNull(reader.GetOrdinal("caption")) ? null : reader.GetString(reader.GetOrdinal("caption")),
                AltText = reader.IsDBNull(reader.GetOrdinal("alt_text")) ? null : reader.GetString(reader.GetOrdinal("alt_text")),
                ImportedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("imported_at")))
            });
        }

        return media;
    }

    private async Task<List<ContentComment>> LoadCommentsAsync(
        SqliteConnection connection,
        string contentId,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM content_comments WHERE content_id = @ContentId AND is_deleted = 0 ORDER BY posted_at";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@ContentId", contentId);

        var comments = new List<ContentComment>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            comments.Add(new ContentComment
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                ContentId = reader.GetString(reader.GetOrdinal("content_id")),
                ParentCommentId = reader.IsDBNull(reader.GetOrdinal("parent_comment_id")) ? null : reader.GetString(reader.GetOrdinal("parent_comment_id")),
                SourceType = reader.GetString(reader.GetOrdinal("source_type")),
                SourceCommentId = reader.GetString(reader.GetOrdinal("source_comment_id")),
                AuthorName = reader.GetString(reader.GetOrdinal("author_name")),
                AuthorId = reader.IsDBNull(reader.GetOrdinal("author_id")) ? null : reader.GetString(reader.GetOrdinal("author_id")),
                AuthorAvatarUrl = reader.IsDBNull(reader.GetOrdinal("author_avatar_url")) ? null : reader.GetString(reader.GetOrdinal("author_avatar_url")),
                CommentText = reader.GetString(reader.GetOrdinal("comment_text")),
                CommentHtml = reader.IsDBNull(reader.GetOrdinal("comment_html")) ? null : reader.GetString(reader.GetOrdinal("comment_html")),
                LikeCount = reader.GetInt32(reader.GetOrdinal("like_count")),
                ReplyCount = reader.GetInt32(reader.GetOrdinal("reply_count")),
                PostedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("posted_at"))),
                ImportedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("imported_at"))),
                IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
                Sentiment = reader.IsDBNull(reader.GetOrdinal("sentiment")) ? null : reader.GetString(reader.GetOrdinal("sentiment")),
                IsQuestion = reader.GetInt32(reader.GetOrdinal("is_question")) == 1,
                RequiresResponse = reader.GetInt32(reader.GetOrdinal("requires_response")) == 1
            });
        }

        return comments;
    }
}
