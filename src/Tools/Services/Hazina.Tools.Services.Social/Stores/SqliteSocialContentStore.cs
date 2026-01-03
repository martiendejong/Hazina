using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Stores;

/// <summary>
/// SQLite-based storage for imported social content.
/// Provides structured storage with full-text search.
/// </summary>
public class SqliteSocialContentStore : ISocialContentStore
{
    private readonly ILogger<SqliteSocialContentStore> _logger;
    private readonly Func<string, string> _databasePathResolver;

    public SqliteSocialContentStore(
        ILogger<SqliteSocialContentStore> logger,
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
            CREATE TABLE IF NOT EXISTS social_posts (
                id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL,
                url TEXT,
                media_urls TEXT,
                like_count INTEGER DEFAULT 0,
                comment_count INTEGER DEFAULT 0,
                share_count INTEGER DEFAULT 0,
                comments TEXT,
                metadata TEXT,
                imported_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_posts_account ON social_posts(account_id);
            CREATE INDEX IF NOT EXISTS idx_posts_created ON social_posts(created_at);

            CREATE TABLE IF NOT EXISTS social_articles (
                id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                summary TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT,
                url TEXT,
                cover_image_url TEXT,
                tags TEXT,
                view_count INTEGER DEFAULT 0,
                like_count INTEGER DEFAULT 0,
                comment_count INTEGER DEFAULT 0,
                metadata TEXT,
                imported_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_articles_account ON social_articles(account_id);
            CREATE INDEX IF NOT EXISTS idx_articles_created ON social_articles(created_at);

            -- Full-text search tables
            CREATE VIRTUAL TABLE IF NOT EXISTS social_posts_fts USING fts5(
                id,
                content,
                content='social_posts',
                content_rowid='rowid'
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS social_articles_fts USING fts5(
                id,
                title,
                content,
                content='social_articles',
                content_rowid='rowid'
            );
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SavePostsAsync(
        string projectId,
        string accountId,
        IEnumerable<SocialPost> posts,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var post in posts)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO social_posts
                    (id, account_id, content, created_at, url, media_urls, like_count, comment_count, share_count, comments, metadata, imported_at)
                    VALUES (@id, @account_id, @content, @created_at, @url, @media_urls, @like_count, @comment_count, @share_count, @comments, @metadata, @imported_at)";

                cmd.Parameters.AddWithValue("@id", post.Id);
                cmd.Parameters.AddWithValue("@account_id", accountId);
                cmd.Parameters.AddWithValue("@content", post.Content);
                cmd.Parameters.AddWithValue("@created_at", post.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("@url", (object?)post.Url ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@media_urls", JsonSerializer.Serialize(post.MediaUrls));
                cmd.Parameters.AddWithValue("@like_count", post.LikeCount);
                cmd.Parameters.AddWithValue("@comment_count", post.CommentCount);
                cmd.Parameters.AddWithValue("@share_count", post.ShareCount);
                cmd.Parameters.AddWithValue("@comments", JsonSerializer.Serialize(post.Comments));
                cmd.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(post.Metadata));
                cmd.Parameters.AddWithValue("@imported_at", DateTime.UtcNow.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Update FTS index
                using var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = @"
                    INSERT OR REPLACE INTO social_posts_fts (id, content)
                    VALUES (@id, @content)";
                ftsCmd.Parameters.AddWithValue("@id", post.Id);
                ftsCmd.Parameters.AddWithValue("@content", post.Content);
                await ftsCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Saved {Count} posts to project {ProjectId}", posts.Count(), projectId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveArticlesAsync(
        string projectId,
        string accountId,
        IEnumerable<SocialArticle> articles,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var article in articles)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO social_articles
                    (id, account_id, title, content, summary, created_at, updated_at, url, cover_image_url, tags, view_count, like_count, comment_count, metadata, imported_at)
                    VALUES (@id, @account_id, @title, @content, @summary, @created_at, @updated_at, @url, @cover_image_url, @tags, @view_count, @like_count, @comment_count, @metadata, @imported_at)";

                cmd.Parameters.AddWithValue("@id", article.Id);
                cmd.Parameters.AddWithValue("@account_id", accountId);
                cmd.Parameters.AddWithValue("@title", article.Title);
                cmd.Parameters.AddWithValue("@content", article.Content);
                cmd.Parameters.AddWithValue("@summary", (object?)article.Summary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@created_at", article.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("@updated_at", article.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@url", (object?)article.Url ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cover_image_url", (object?)article.CoverImageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(article.Tags));
                cmd.Parameters.AddWithValue("@view_count", article.ViewCount);
                cmd.Parameters.AddWithValue("@like_count", article.LikeCount);
                cmd.Parameters.AddWithValue("@comment_count", article.CommentCount);
                cmd.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(article.Metadata));
                cmd.Parameters.AddWithValue("@imported_at", DateTime.UtcNow.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Update FTS index
                using var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = @"
                    INSERT OR REPLACE INTO social_articles_fts (id, title, content)
                    VALUES (@id, @title, @content)";
                ftsCmd.Parameters.AddWithValue("@id", article.Id);
                ftsCmd.Parameters.AddWithValue("@title", article.Title);
                ftsCmd.Parameters.AddWithValue("@content", article.Content);
                await ftsCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Saved {Count} articles to project {ProjectId}", articles.Count(), projectId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<SocialPost>> GetPostsAsync(
        string projectId,
        string? accountId = null,
        DateTime? since = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = "SELECT * FROM social_posts WHERE 1=1";
        if (accountId != null) sql += " AND account_id = @account_id";
        if (since.HasValue) sql += " AND created_at >= @since";
        sql += " ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        if (accountId != null) cmd.Parameters.AddWithValue("@account_id", accountId);
        if (since.HasValue) cmd.Parameters.AddWithValue("@since", since.Value.ToString("O"));
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var posts = new List<SocialPost>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            posts.Add(ReadPost(reader));
        }

        return posts;
    }

    public async Task<List<SocialArticle>> GetArticlesAsync(
        string projectId,
        string? accountId = null,
        DateTime? since = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var sql = "SELECT * FROM social_articles WHERE 1=1";
        if (accountId != null) sql += " AND account_id = @account_id";
        if (since.HasValue) sql += " AND created_at >= @since";
        sql += " ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        if (accountId != null) cmd.Parameters.AddWithValue("@account_id", accountId);
        if (since.HasValue) cmd.Parameters.AddWithValue("@since", since.Value.ToString("O"));
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var articles = new List<SocialArticle>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            articles.Add(ReadArticle(reader));
        }

        return articles;
    }

    public async Task<List<SocialContentSearchResult>> SearchAsync(
        string projectId,
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SearchOptions();
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        var results = new List<SocialContentSearchResult>();

        // Search posts
        if (options.ContentTypes.Contains("posts"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT p.*, highlight(social_posts_fts, 1, '<mark>', '</mark>') as snippet
                FROM social_posts p
                JOIN social_posts_fts fts ON p.id = fts.id
                WHERE social_posts_fts MATCH @query
                ORDER BY rank
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@query", query);
            cmd.Parameters.AddWithValue("@limit", options.Limit);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var post = ReadPost(reader);
                results.Add(new SocialContentSearchResult
                {
                    ContentType = "post",
                    Id = post.Id,
                    AccountId = post.AccountId,
                    Text = post.Content,
                    Snippet = reader.GetString(reader.GetOrdinal("snippet")),
                    CreatedAt = post.CreatedAt,
                    Url = post.Url,
                    Score = 1.0f // FTS doesn't provide a normalized score
                });
            }
        }

        // Search articles
        if (options.ContentTypes.Contains("articles"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.*, highlight(social_articles_fts, 2, '<mark>', '</mark>') as snippet
                FROM social_articles a
                JOIN social_articles_fts fts ON a.id = fts.id
                WHERE social_articles_fts MATCH @query
                ORDER BY rank
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@query", query);
            cmd.Parameters.AddWithValue("@limit", options.Limit);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var article = ReadArticle(reader);
                results.Add(new SocialContentSearchResult
                {
                    ContentType = "article",
                    Id = article.Id,
                    AccountId = article.AccountId,
                    Text = article.Title,
                    Snippet = reader.GetString(reader.GetOrdinal("snippet")),
                    CreatedAt = article.CreatedAt,
                    Url = article.Url,
                    Score = 1.0f
                });
            }
        }

        return results;
    }

    public async Task DeleteAccountContentAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = "DELETE FROM social_posts WHERE account_id = @account_id";
            cmd1.Parameters.AddWithValue("@account_id", accountId);
            await cmd1.ExecuteNonQueryAsync(cancellationToken);

            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = "DELETE FROM social_articles WHERE account_id = @account_id";
            cmd2.Parameters.AddWithValue("@account_id", accountId);
            await cmd2.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Deleted all content for account {AccountId} in project {ProjectId}",
                accountId, projectId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContentStats> GetStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);
        var stats = new ContentStats();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT
                (SELECT COUNT(*) FROM social_posts) as total_posts,
                (SELECT COUNT(*) FROM social_articles) as total_articles,
                (SELECT MIN(created_at) FROM social_posts) as oldest_post,
                (SELECT MAX(created_at) FROM social_posts) as newest_post";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            stats.TotalPosts = reader.GetInt32(0);
            stats.TotalArticles = reader.GetInt32(1);
            var oldestStr = reader.IsDBNull(2) ? null : reader.GetString(2);
            var newestStr = reader.IsDBNull(3) ? null : reader.GetString(3);
            if (oldestStr != null) stats.OldestContent = DateTime.Parse(oldestStr);
            if (newestStr != null) stats.NewestContent = DateTime.Parse(newestStr);
        }

        // Get posts by account
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT account_id, COUNT(*) FROM social_posts GROUP BY account_id";
        await using var reader2 = await cmd2.ExecuteReaderAsync(cancellationToken);
        while (await reader2.ReadAsync(cancellationToken))
        {
            stats.PostsByAccount[reader2.GetString(0)] = reader2.GetInt32(1);
        }

        return stats;
    }

    private static SocialPost ReadPost(SqliteDataReader reader)
    {
        return new SocialPost
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            AccountId = reader.GetString(reader.GetOrdinal("account_id")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            Url = reader.IsDBNull(reader.GetOrdinal("url")) ? null : reader.GetString(reader.GetOrdinal("url")),
            MediaUrls = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("media_urls"))) ?? new(),
            LikeCount = reader.GetInt32(reader.GetOrdinal("like_count")),
            CommentCount = reader.GetInt32(reader.GetOrdinal("comment_count")),
            ShareCount = reader.GetInt32(reader.GetOrdinal("share_count")),
            Comments = JsonSerializer.Deserialize<List<SocialComment>>(reader.GetString(reader.GetOrdinal("comments"))) ?? new(),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("metadata"))) ?? new()
        };
    }

    private static SocialArticle ReadArticle(SqliteDataReader reader)
    {
        return new SocialArticle
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            AccountId = reader.GetString(reader.GetOrdinal("account_id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            Summary = reader.IsDBNull(reader.GetOrdinal("summary")) ? null : reader.GetString(reader.GetOrdinal("summary")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            Url = reader.IsDBNull(reader.GetOrdinal("url")) ? null : reader.GetString(reader.GetOrdinal("url")),
            CoverImageUrl = reader.IsDBNull(reader.GetOrdinal("cover_image_url")) ? null : reader.GetString(reader.GetOrdinal("cover_image_url")),
            Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("tags"))) ?? new(),
            ViewCount = reader.GetInt32(reader.GetOrdinal("view_count")),
            LikeCount = reader.GetInt32(reader.GetOrdinal("like_count")),
            CommentCount = reader.GetInt32(reader.GetOrdinal("comment_count")),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("metadata"))) ?? new()
        };
    }
}
