namespace Ticky.Internal.Services;

public class CardNumberingService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public CardNumberingService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<int> GetNextNumberAsync(int boardId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return (
                await db
                    .Cards.Include(x => x.Column)
                    .Where(x => x.Column.BoardId.Equals(boardId))
                    .MaxAsync(x => (int?)x.Number) ?? 0
            ) + 1;
    }
}
