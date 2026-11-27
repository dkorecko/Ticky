using System.Drawing;
using System.Text.RegularExpressions;
using Ticky.Base.Entities.Owned;

namespace Ticky.Internal.Services;

/// <summary>
/// Creating, changing, cloning and deleting boards, for both the web UI and the API. Project admins add boards to
/// their projects; board admins (see <see cref="Board.IsAdmin(int)"/>) change and delete them. Roles are read from
/// the database on every call, so a role taken away while a page is open stops working at once.
/// </summary>
public class BoardService(IDbContextFactory<DataContext> dbContextFactory)
{
    private const string CODE_FORMAT = "The code must be 1 to 5 upper-case letters, like TKY.";
    private static readonly Regex CodePattern = new("^[A-Z]{1,5}$");

    /// <summary>A new board's name, description and the code its cards' keys start with, like <c>TKY</c>.</summary>
    public record BoardDetails(string Name, string Description, string Code);

    /// <summary>Changes to a board; null leaves a value as it is.</summary>
    public record BoardChanges
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Code { get; init; }
        public bool? DisableSortingAnimations { get; init; }
    }

    /// <summary>Adds a board to a project the user administers, with the default labels Bug, Feature and Enhancement.</summary>
    public async Task<OperationResult<Board>> CreateAsync(int projectId, int userId, BoardDetails details)
    {
        var name = details.Name?.Trim() ?? string.Empty;
        var code = details.Code?.Trim() ?? string.Empty;

        if ((OperationResult.RequireText(name, nameof(Board.Name)) ?? ValidateCode(code)) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, "add boards to the project") is { } denied)
            return denied;

        var board = new Board
        {
            Name = name,
            Description = details.Description?.Trim() ?? string.Empty,
            Code = code,
            ProjectId = projectId,
        };

        board.Labels.AddRange(
            [
                new Label { Name = "Bug", TextColor = Color.White, BackgroundColor = Color.FromArgb(220, 38, 38), BoardId = 0 },
                new Label { Name = "Feature", TextColor = Color.White, BackgroundColor = Color.FromArgb(16, 185, 129), BoardId = 0 },
                new Label { Name = "Enhancement", TextColor = Color.White, BackgroundColor = Color.FromArgb(59, 130, 246), BoardId = 0 },
            ]
        );

        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    /// <summary>Changes the board's name, description, code or settings. Only its admins may.</summary>
    public async Task<OperationResult<Board>> UpdateAsync(int boardId, int userId, BoardChanges changes)
    {
        var name = changes.Name?.Trim();
        var code = changes.Code?.Trim();

        if (((name is null ? null : OperationResult.RequireText(name, nameof(Board.Name))) ?? (code is null ? null : ValidateCode(code))) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, "edit the board") is { } denied)
            return denied;

        var board = await db.Boards.FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
            return OperationResult.NotFound(BoardAccessHelper.BOARD_NOT_FOUND);

        board.Name = name ?? board.Name;
        board.Description = changes.Description?.Trim() ?? board.Description;
        board.Code = code ?? board.Code;
        board.DisableSortingAnimations = changes.DisableSortingAnimations ?? board.DisableSortingAnimations;

        await db.SaveChangesAsync();
        return board;
    }

    /// <summary>Deletes the board with all its columns, cards and labels. Only its admins may.</summary>
    public async Task<OperationResult> DeleteAsync(int boardId, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, "delete the board") is { } denied)
            return denied;

        await db.Boards.Where(x => x.Id == boardId).ExecuteDeleteAsync();
        return OperationResult.Success;
    }

    /// <summary>Marks the board as one of the user's favorites, which the home page lists first, or unmarks it.</summary>
    public async Task<OperationResult> SetFavoriteAsync(int boardId, int userId, bool favorite)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.GetBoardAccessAsync(boardId, userId) == AccessLevel.None)
            return OperationResult.NotFound(BoardAccessHelper.BOARD_NOT_FOUND);

        var existing = await db.Favorites.FirstOrDefaultAsync(x => x.BoardId == boardId && x.UserId == userId);

        if (favorite && existing is null)
            db.Favorites.Add(new Favorite { BoardId = boardId, UserId = userId });
        else if (!favorite && existing is not null)
            db.Favorites.Remove(existing);

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    /// <summary>
    /// Copies the board into a project the user administers: its board members, labels, columns and cards with their
    /// labels, assignees, subtasks, reminders and repeats. Comments, attachments, time records, links and activity
    /// stay with the original. The copy is named "Copy of …". Only admins of the board may clone it.
    /// </summary>
    public async Task<OperationResult<Board>> CloneAsync(int boardId, int userId, int targetProjectId, string code)
    {
        code = code?.Trim() ?? string.Empty;

        if (ValidateCode(code) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, "clone the board") is { } denied)
            return denied;

        if (await db.GetProjectAccessAsync(targetProjectId, userId) != AccessLevel.Admin)
            return OperationResult.Invalid("TargetProjectId", "You can only clone into projects you administer.");

        var board = await db.Boards
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Labels)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Assignees)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Subtasks)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Reminders)
            .Include(x => x.Labels)
            .Include(x => x.Memberships)
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
            return OperationResult.NotFound(BoardAccessHelper.BOARD_NOT_FOUND);

        var newBoard = new Board
        {
            Name = $"Copy of {board.Name}",
            Description = board.Description,
            Code = code,
            ProjectId = targetProjectId,
            DisableSortingAnimations = board.DisableSortingAnimations,
        };

        foreach (var membership in board.Memberships)
        {
            newBoard.Memberships.Add(new BoardMembership
            {
                BoardId = 0,
                UserId = membership.UserId,
                AddedAt = membership.AddedAt,
                IsAdmin = membership.IsAdmin,
            });
        }

        var labels = board.Labels.ToDictionary(
            x => x.Id,
            x => new Label { Name = x.Name, TextColor = x.TextColor, BackgroundColor = x.BackgroundColor, BoardId = 0 }
        );
        newBoard.Labels.AddRange(labels.Values);

        // The copies reference the same users. They're loaded again, tracked: the untracked ones above lead back into
        // the original board.
        var assigneeIds = board.Columns.SelectMany(x => x.Cards).SelectMany(x => x.Assignees).Select(x => x.Id).Distinct().ToList();
        var users = await db.Users.Where(x => assigneeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var columns = new Dictionary<int, Column>();
        var repeats = new List<(Card Copy, RepeatInfo Repeat)>();

        foreach (var column in board.Columns.OrderBy(x => x.Index))
        {
            var newColumn = new Column
            {
                Name = column.Name,
                Index = column.Index,
                MaxCards = column.MaxCards,
                Finished = column.Finished,
                Collapsed = column.Collapsed,
                NewCardPlacement = column.NewCardPlacement,
#pragma warning disable CS0618 // Copied as is until the setting is removed.
                OrderRule = column.OrderRule,
#pragma warning restore CS0618
                BoardId = 0,
            };

            foreach (var card in column.Cards.OrderBy(x => x.Index))
            {
                var newCard = new Card
                {
                    Name = card.Name,
                    Description = card.Description,
                    Number = card.Number,
                    Index = card.Index,
                    Flagged = card.Flagged,
                    Deadline = card.Deadline,
                    DeadlineProcessed = card.DeadlineProcessed,
                    Priority = card.Priority,
                    CreatedById = card.CreatedById,
                    CreatedAt = DateTime.Now,
                    ColumnId = 0,
                    SnoozedUntil = card.SnoozedUntil,
                };

                newCard.Labels.AddRange(card.Labels.Where(x => labels.ContainsKey(x.Id)).Select(x => labels[x.Id]));
                newCard.Assignees.AddRange(card.Assignees.Select(x => users[x.Id]));

                foreach (var subtask in card.Subtasks.OrderBy(x => x.Index))
                    newCard.Subtasks.Add(new Subtask { Text = subtask.Text, Completed = subtask.Completed, Index = subtask.Index, CardId = 0 });

                foreach (var reminder in card.Reminders)
                    newCard.Reminders.Add(new Reminder { At = reminder.At, CardId = 0 });

                if (card.RepeatInfo is not null)
                    repeats.Add((newCard, card.RepeatInfo));

                newColumn.Cards.Add(newCard);
            }

            columns[column.Id] = newColumn;
            newBoard.Columns.Add(newColumn);
        }

        db.Boards.Add(newBoard);
        await db.SaveChangesAsync();

        // A repeat's target column becomes the copy's column, which only has an id now.
        foreach (var (copy, repeat) in repeats)
        {
            copy.RepeatInfo = new RepeatInfo
            {
                CardPlacement = repeat.CardPlacement,
                LastRepeat = repeat.LastRepeat,
                Number = repeat.Number,
                Time = repeat.Time,
                Type = repeat.Type,
                Selected = repeat.Selected,
                TargetColumnId = repeat.TargetColumnId is { } id && columns.TryGetValue(id, out var target) ? target.Id : null,
            };
        }

        await db.SaveChangesAsync();
        return newBoard;
    }

    private static OperationResult? ValidateCode(string code) =>
        CodePattern.IsMatch(code) ? null : OperationResult.Invalid(nameof(Board.Code), CODE_FORMAT);
}
