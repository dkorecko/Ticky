namespace Ticky.Internal.Services;

public class SearchService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public SearchService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Card>> SearchAsync(
        string? query,
        User user,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        using var db = _dbContextFactory.CreateDbContext();

        return await db
            .Cards.Include(x => x.Column)
            .ThenInclude(x => x.Board)
            .ThenInclude(x => x.Project)
            .ThenInclude(x => x.Memberships)
            .Include(x => x.Column)
            .ThenInclude(x => x.Board)
            .ThenInclude(x => x.Memberships)
            .Where(x =>
                x.Column.Board.Project.Memberships.Any(membership =>
                    membership.UserId.Equals(user.Id)
                ) || x.Column.Board.Memberships.Any(membership => membership.UserId.Equals(user.Id))
            )
            .Where(x =>
                x.Name.Contains(query) || (x.Column.Board.Code + "-" + x.Number).Contains(query)
            )
            .Take(5)
            .AsNoTracking()
            .ToListAsync();
    }
}
