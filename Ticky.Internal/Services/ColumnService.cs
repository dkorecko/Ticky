namespace Ticky.Internal.Services;

/// <summary>
/// Adding, changing, reordering and deleting a board's columns, for both the web UI and the API. Only board admins
/// may (see <see cref="Board.IsAdmin(int)"/>); the role is read from the database on every call.
/// </summary>
public class ColumnService(IDbContextFactory<DataContext> dbContextFactory)
{
    private const string COLUMN_NOT_FOUND = "The column doesn't exist, or you can't access it.";
    private const string MANAGE_COLUMNS = "manage columns";

    /// <summary>A new column's settings.</summary>
    public record ColumnSettings(string Name)
    {
        /// <summary>Cards in the column count as done, e.g. for the burndown chart and Your work.</summary>
        public bool Finished { get; init; }

        /// <summary>The most cards the column may hold; 0 for no limit.</summary>
        public int MaxCards { get; init; }

        public CardPlacement NewCardPlacement { get; init; } = CardPlacement.Bottom;
    }

    /// <summary>Changes to a column; null leaves a value as it is.</summary>
    public record ColumnChanges
    {
        public string? Name { get; init; }
        public bool? Finished { get; init; }
        public int? MaxCards { get; init; }
        public CardPlacement? NewCardPlacement { get; init; }

        /// <summary>Shows the column narrow, without its cards. Applies to everyone viewing the board.</summary>
        public bool? Collapsed { get; init; }
    }

    /// <summary>Adds a column after the board's last one.</summary>
    public async Task<OperationResult<Column>> CreateAsync(int boardId, int userId, ColumnSettings settings)
    {
        var name = settings.Name?.Trim() ?? string.Empty;

        if (Validate(name, settings.MaxCards, settings.NewCardPlacement) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, MANAGE_COLUMNS) is { } denied)
            return denied;

        var columns = await db.Columns.Where(x => x.BoardId == boardId).ToListAsync();
        var column = new Column
        {
            Name = name,
            BoardId = boardId,
            Index = columns.GetNextIndex(),
            Finished = settings.Finished,
            MaxCards = settings.MaxCards,
            NewCardPlacement = settings.NewCardPlacement,
        };

        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column;
    }

    public async Task<OperationResult<Column>> UpdateAsync(int columnId, int userId, ColumnChanges changes)
    {
        var name = changes.Name?.Trim();

        if (Validate(name, changes.MaxCards, changes.NewCardPlacement) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();
        var column = await db.Columns.FirstOrDefaultAsync(x => x.Id == columnId);

        if (await db.CheckBoardAdminAsync(column?.BoardId, userId, MANAGE_COLUMNS, COLUMN_NOT_FOUND) is { } denied)
            return denied;

        column!.Name = name ?? column.Name;
        column.Finished = changes.Finished ?? column.Finished;
        column.MaxCards = changes.MaxCards ?? column.MaxCards;
        column.NewCardPlacement = changes.NewCardPlacement ?? column.NewCardPlacement;
        column.Collapsed = changes.Collapsed ?? column.Collapsed;

        await db.SaveChangesAsync();
        return column;
    }

    /// <summary>Puts the column before another column of its board, or last.</summary>
    public async Task<OperationResult> MoveAsync(int columnId, int userId, int? beforeColumnId)
    {
        using var db = dbContextFactory.CreateDbContext();
        var column = await db.Columns.Include(x => x.Board.Columns).FirstOrDefaultAsync(x => x.Id == columnId);

        if (await db.CheckBoardAdminAsync(column?.BoardId, userId, MANAGE_COLUMNS, COLUMN_NOT_FOUND) is { } denied)
            return denied;

        var columns = column!.Board.Columns;
        columns.MoveBefore(column, columns.FirstOrDefault(x => x.Id == beforeColumnId));

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    /// <summary>Deletes the column with its cards, and closes the gap it leaves.</summary>
    public async Task<OperationResult> DeleteAsync(int columnId, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();
        var column = await db.Columns.Include(x => x.Board.Columns).FirstOrDefaultAsync(x => x.Id == columnId);

        if (await db.CheckBoardAdminAsync(column?.BoardId, userId, MANAGE_COLUMNS, COLUMN_NOT_FOUND) is { } denied)
            return denied;

        db.Columns.Remove(column!);
        column!.Board.Columns.Where(x => x != column).ToList().FixIndices();

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    /// <summary>Checks the values that are set.</summary>
    private static OperationResult? Validate(string? name, int? maxCards, CardPlacement? placement)
    {
        if (name is not null && OperationResult.RequireText(name, nameof(Column.Name)) is { } empty)
            return empty;

        if (maxCards < 0)
            return OperationResult.Invalid(nameof(Column.MaxCards), "The card limit can't be negative; use 0 for no limit.");

        if (placement is { } value && !Enum.IsDefined(value))
            return OperationResult.Invalid(nameof(Column.NewCardPlacement), "Unknown card placement.");

        return null;
    }
}
