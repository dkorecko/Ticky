namespace Ticky.Internal.Services;

/// <summary>Finds cards on every board the user can open, by name or key (see <see cref="CardQueryHelper.Search"/>).</summary>
public class SearchService
{
    /// <summary>How many results the search box shows.</summary>
    public const int DEFAULT_LIMIT = 5;

    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public SearchService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>The best matches, with their column, board and project loaded for showing where they are.</summary>
    public async Task<List<Card>> SearchAsync(
        string? query,
        int userId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        using var db = _dbContextFactory.CreateDbContext();

        return await db.Cards
            .AccessibleBy(userId)
            .Search(query)
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
                    .ThenInclude(x => x.Project)
            .Take(DEFAULT_LIMIT)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
