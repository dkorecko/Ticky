using Ticky.Base.Entities.Owned;

namespace Ticky.Internal.Services;

/// <summary>
/// Everything people do to cards, for both the web UI and the API. Every method checks that the user can open the
/// card's board (see <see cref="Board.AccessibleBy"/>), so no caller can reach a card by guessing its id. The exception
/// is <see cref="StopTimerAsync"/>, which only touches the caller's own timer. Most changes
/// also write an entry to the card's activity log; a change that would leave things as they are does nothing, so the
/// log only shows real changes.
/// </summary>
/// <remarks>
/// Items of a card (subtasks, reminders, attachments, time records, links) are addressed by the card and the item's
/// id together, and an item of another card counts as missing. Activities store what happened as data; the text is
/// built when shown (see <see cref="ActivityFormatter"/>).
/// </remarks>
public class CardService
{
    public const string CARD_NOT_FOUND = "The card doesn't exist, or you can't access it.";
    private const string COLUMN_NOT_FOUND = "The column doesn't exist, or you can't access it.";
    private const string COLUMN_FULL = "The column is full.";
    private const string NOT_A_MEMBER = "Only members of the board can be assigned.";
    private const string NOT_A_BOARD_LABEL = "The label isn't one of the board's labels.";
    public const string REPEAT_COLUMN_ELSEWHERE = "The target column has to be on this card's board.";

    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly CardNumberingService _cardNumberingService;

    public CardService(
        IDbContextFactory<DataContext> dbContextFactory,
        CardNumberingService cardNumberingService
    )
    {
        _dbContextFactory = dbContextFactory;
        _cardNumberingService = cardNumberingService;
    }

    /// <summary>A new card. Only the name is required; the lists hold ids of the board's labels and members.</summary>
    public record CardDraft(string Name)
    {
        public string? Description { get; init; }
        public CardPriority? Priority { get; init; }
        public DateTime? Deadline { get; init; }
        public IReadOnlyCollection<int> LabelIds { get; init; } = [];
        public IReadOnlyCollection<int> AssigneeIds { get; init; } = [];
    }

    /// <summary>
    /// Creates a card in the column, at the top or bottom as the column's placement setting says. Creating a card
    /// isn't written to the activity log, and neither are the values it starts with.
    /// </summary>
    public async Task<OperationResult<Card>> CreateCardAsync(int columnId, int userId, CardDraft draft)
    {
        var name = draft.Name?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(name, nameof(CardDraft.Name)) is { } empty)
            return empty;

        using var db = _dbContextFactory.CreateDbContext();

        var column = await db.Columns
            .AccessibleBy(userId)
            .Include(x => x.Cards)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
            return OperationResult.NotFound(COLUMN_NOT_FOUND);

        if (column.IsFull())
            return OperationResult.Conflict(COLUMN_FULL);

        var labelIds = draft.LabelIds.Distinct().ToList();
        var labels = await db.Labels.Where(x => x.BoardId == column.BoardId && labelIds.Contains(x.Id)).ToListAsync();

        if (labels.Count != labelIds.Count)
            return OperationResult.Invalid(nameof(CardDraft.LabelIds), "Every label must be one of the board's labels.");

        var assigneeIds = draft.AssigneeIds.Distinct().ToList();
        var assignees = await db.Users.MembersOf(column.BoardId).Where(x => assigneeIds.Contains(x.Id)).ToListAsync();

        if (assignees.Count != assigneeIds.Count)
            return OperationResult.Invalid(nameof(CardDraft.AssigneeIds), "Every assignee must be a member of the board.");

        var card = new Card
        {
            Name = name,
            Description = draft.Description ?? string.Empty,
            Priority = draft.Priority ?? CardPriority.Normal,
            Deadline = draft.Deadline,
            ColumnId = columnId,
            Index = 0,
            CreatedAt = DateTime.Now,
            Number = await _cardNumberingService.GetNextNumberAsync(column.BoardId),
            CreatedById = userId,
        };

        card.Labels.AddRange(labels);
        card.Assignees.AddRange(assignees);
        column.Cards.Place(card, column.NewCardPlacement);

        await db.SaveChangesAsync();
        return card;
    }

    /// <summary>
    /// Whether the user may see and edit the card: they must be able to open its board (see
    /// <see cref="Board.AccessibleBy"/>). False if the card does not exist.
    /// </summary>
    public async Task<bool> CanAccessCardAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return await db.Cards.AccessibleBy(userId).AnyAsync(x => x.Id == cardId);
    }

    /// <summary>Deletes the card with everything on it, and closes the gap it leaves in its column.</summary>
    public async Task<OperationResult> DeleteAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AccessibleBy(userId).Include(x => x.Column.Cards).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        db.Cards.Remove(card);
        card.Column.Cards.Where(x => x != card).ToList().FixIndices();

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    #region Card fields

    public Task<OperationResult> RenameAsync(int cardId, int userId, string name)
    {
        name = name?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(name, "Name") is { } empty)
            return Task.FromResult(empty);

        return UpdateAsync(
            cardId,
            userId,
            card => new TitleChangedActivity { OldTitle = card.Name, NewTitle = name },
            card => card.Name = name,
            unchanged: card => card.Name == name
        );
    }

    public Task<OperationResult> UpdateDescriptionAsync(int cardId, int userId, string description) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new DescriptionChangedActivity(),
            card => card.Description = description,
            unchanged: card => card.Description == description
        );

    public Task<OperationResult> SetPriorityAsync(int cardId, int userId, CardPriority priority) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new PriorityChangedActivity { Priority = priority },
            card => card.Priority = priority,
            unchanged: card => card.Priority == priority
        );

    /// <summary>Flags or unflags the card. Not written to the activity log, as flagging never was.</summary>
    public Task<OperationResult> SetFlaggedAsync(int cardId, int userId, bool flagged) =>
        UpdateAsync(cardId, userId, activity: null, card => card.Flagged = flagged, unchanged: card => card.Flagged == flagged);

    /// <summary>
    /// Sets the deadline and arms its reminder again. Setting the deadline the card already has does nothing, so a
    /// reminder that already went out isn't sent again.
    /// </summary>
    public Task<OperationResult> SetDeadlineAsync(int cardId, int userId, DateTime deadline) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new DeadlineSetActivity { Deadline = deadline },
            card =>
            {
                card.Deadline = deadline;
                card.DeadlineProcessed = false;
            },
            unchanged: card => card.Deadline == deadline
        );

    public Task<OperationResult> RemoveDeadlineAsync(int cardId, int userId) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new DeadlineRemovedActivity(),
            card => card.Deadline = null,
            unchanged: card => card.Deadline is null
        );

    /// <summary>Hides the card from the board until the time; then it comes back on its own.</summary>
    public Task<OperationResult> SnoozeAsync(int cardId, int userId, DateTime until) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new SnoozedActivity { Until = until },
            card => card.SnoozedUntil = until,
            unchanged: card => card.SnoozedUntil == until
        );

    public Task<OperationResult> UnsnoozeAsync(int cardId, int userId) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new UnsnoozedActivity(),
            card => card.SnoozedUntil = null,
            unchanged: card => card.SnoozedUntil is null
        );

    /// <summary>
    /// Makes the card repeat: on the schedule, a copy is created in <see cref="RepeatInfo.TargetColumnId"/>, which has
    /// to be a column of the card's own board. The repeat job's fallback to the board's first column only exists for
    /// repeats saved before a target was required.
    /// </summary>
    public async Task<OperationResult> SetRepeatAsync(int cardId, int userId, RepeatInfo repeat)
    {
        if (ValidateRepeat(repeat) is { } invalid)
            return invalid;

        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AccessibleBy(userId).Include(x => x.Column).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        if (repeat.TargetColumnId is not { } targetColumnId)
            return OperationResult.Invalid(nameof(RepeatInfo.TargetColumnId), "Pick the column the copies go to.");

        if (!await db.Columns.AnyAsync(x => x.Id == targetColumnId && x.BoardId == card.Column.BoardId))
            return OperationResult.Invalid(nameof(RepeatInfo.TargetColumnId), REPEAT_COLUMN_ELSEWHERE);

        card.RepeatInfo = repeat;
        Log(db, cardId, userId, new RepeatSetActivity { RepeatType = repeat.Type });

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    public Task<OperationResult> RemoveRepeatAsync(int cardId, int userId) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new RepeatDisabledActivity(),
            card => card.RepeatInfo = null,
            unchanged: card => card.RepeatInfo is null
        );

    /// <summary>Checks a repeat's settings (see <see cref="RepeatInfo.Validate"/>), and clears the ones its type doesn't use.</summary>
    private static OperationResult? ValidateRepeat(RepeatInfo repeat)
    {
        if (repeat.Validate() is { } problem)
            return OperationResult.Invalid(problem.Field, problem.Message);

        if (!Enum.IsDefined(repeat.CardPlacement))
            return OperationResult.Invalid(nameof(RepeatInfo.CardPlacement), "Unknown card placement.");

        repeat.ClearUnused();
        return null;
    }

    #endregion

    #region Moving

    /// <summary>
    /// Moves the card to a column on its board, before <paramref name="beforeCardId"/> or at the end when that is null
    /// or not in the column. In its own column this reorders it, which isn't written to the activity log.
    /// </summary>
    public async Task<OperationResult> MoveAsync(int cardId, int userId, int targetColumnId, int? beforeCardId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AccessibleBy(userId).Include(x => x.Column.Cards).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        var oldColumn = card.Column;
        var targetColumn = targetColumnId == oldColumn.Id
            ? oldColumn
            : await db.Columns
                .Include(x => x.Cards)
                .FirstOrDefaultAsync(x => x.Id == targetColumnId && x.BoardId == oldColumn.BoardId);

        if (targetColumn is null)
            return OperationResult.Invalid("TargetColumnId", "The column isn't on the card's board.");

        var beforeCard = targetColumn.Cards.FirstOrDefault(x => x.Id == beforeCardId);

        if (targetColumn == oldColumn)
        {
            targetColumn.Cards.MoveBefore(card, beforeCard);
            await db.SaveChangesAsync();
            return OperationResult.Success;
        }

        if (targetColumn.IsFull())
            return OperationResult.Conflict(COLUMN_FULL);

        oldColumn.Cards.Remove(card);
        oldColumn.Cards.FixIndices();

        targetColumn.Cards.MoveBefore(card, beforeCard);
        targetColumn.Cards.Add(card);
        card.Column = targetColumn;
        Log(db, cardId, userId, Moved(oldColumn, targetColumn));

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    /// <summary>Moves the card to the end of the column next to its own. Use -1 for the previous column, +1 for the next.</summary>
    public async Task<OperationResult> MoveToAdjacentColumnAsync(int cardId, int userId, int offset)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var column = await db.Cards
            .AccessibleBy(userId)
            .Where(x => x.Id == cardId)
            .Select(x => new { x.Column.BoardId, x.Column.Index })
            .FirstOrDefaultAsync();

        if (column is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        var targetColumnId = await db.Columns
            .Where(x => x.BoardId == column.BoardId && x.Index == column.Index + offset)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (targetColumnId is null)
            return OperationResult.Invalid(nameof(offset), "There is no column there.");

        return await MoveAsync(cardId, userId, targetColumnId.Value, beforeCardId: null);
    }

    #endregion

    #region Assignees and labels

    /// <summary>
    /// Assigns the member to the card, or unassigns them. Only the board's members can be assigned, but anyone can be
    /// unassigned, e.g. after leaving the board.
    /// </summary>
    /// <returns>The card's assignees after the change.</returns>
    public async Task<OperationResult<List<User>>> SetAssigneeAsync(int cardId, int userId, int memberId, bool assigned)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards
            .AccessibleBy(userId)
            .Include(x => x.Assignees)
            .Include(x => x.Column)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        return await SetAssigneeAsync(
            db, card, card.Column.BoardId, cardId, userId, memberId, assigned,
            user => new AssigneeAddedActivity { AssigneeId = user.Id, AssigneeName = user.DisplayName },
            user => new AssigneeRemovedActivity { AssigneeId = user.Id, AssigneeName = user.DisplayName }
        );
    }

    /// <summary>
    /// Assigns the member to a card or subtask, or unassigns them, and logs it with the activity for that. Only the
    /// board's members can be assigned, but anyone can be unassigned, e.g. after leaving the board.
    /// </summary>
    /// <returns>The assignees after the change.</returns>
    private static async Task<OperationResult<List<User>>> SetAssigneeAsync(
        DataContext db,
        IAssignable item,
        int boardId,
        int cardId,
        int userId,
        int memberId,
        bool assigned,
        Func<User, Activity> addedActivity,
        Func<User, Activity> removedActivity
    )
    {
        var assignee = item.Assignees.FirstOrDefault(x => x.Id == memberId);

        if (assigned == (assignee is not null))
            return item.Assignees;

        if (assigned)
        {
            assignee = await db.Users.MembersOf(boardId).FirstOrDefaultAsync(x => x.Id == memberId);

            if (assignee is null)
                return OperationResult.Invalid("UserId", NOT_A_MEMBER);

            item.Assignees.Add(assignee);
            Log(db, cardId, userId, addedActivity(assignee));
        }
        else
        {
            item.Assignees.Remove(assignee!);
            Log(db, cardId, userId, removedActivity(assignee!));
        }

        await db.SaveChangesAsync();
        return item.Assignees;
    }

    /// <summary>Adds one of the board's labels to the card, or removes it.</summary>
    /// <returns>The card's labels after the change.</returns>
    public async Task<OperationResult<List<Label>>> SetLabelAsync(int cardId, int userId, int labelId, bool added)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards
            .AccessibleBy(userId)
            .Include(x => x.Labels)
            .Include(x => x.Column)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        var label = await db.Labels.FirstOrDefaultAsync(x => x.Id == labelId && x.BoardId == card.Column.BoardId);

        if (label is null)
            return OperationResult.Invalid("LabelId", NOT_A_BOARD_LABEL);

        if (added == card.Labels.Contains(label))
            return card.Labels;

        if (added)
        {
            card.Labels.Add(label);
            Log(db, cardId, userId, new LabelAddedActivity { LabelId = label.Id, LabelName = label.Name });
        }
        else
        {
            card.Labels.Remove(label);
            Log(db, cardId, userId, new LabelRemovedActivity { LabelId = label.Id, LabelName = label.Name });
        }

        await db.SaveChangesAsync();
        return card.Labels;
    }

    #endregion

    #region Comments

    public async Task<OperationResult<Comment>> AddCommentAsync(int cardId, int userId, string text)
    {
        text = text?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(text, "Text", "comment") is { } empty)
            return empty;

        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var comment = new Comment
        {
            Text = text,
            CreatedById = userId,
            CardId = cardId,
        };

        db.Comments.Add(comment);
        Log(db, cardId, userId, new CommentPostedActivity());

        await db.SaveChangesAsync();
        return comment;
    }

    #endregion

    #region Subtasks

    private const string SUBTASK_NOT_FOUND = "The subtask doesn't exist, or you can't access it.";

    public async Task<OperationResult<Subtask>> AddSubtaskAsync(int cardId, int userId, string text)
    {
        text = text?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(text, "Text", "subtask") is { } empty)
            return empty;

        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var subtasks = await db.Subtasks.Where(x => x.CardId == cardId).ToListAsync();
        var subtask = new Subtask
        {
            CardId = cardId,
            Text = text,
            Index = subtasks.GetNextIndex(),
        };

        db.Subtasks.Add(subtask);
        Log(db, cardId, userId, new SubtaskAddedActivity { SubtaskText = text });

        await db.SaveChangesAsync();
        return subtask;
    }

    public async Task<OperationResult> EditSubtaskAsync(int cardId, int userId, int subtaskId, string text)
    {
        text = text?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(text, "Text", "subtask") is { } empty)
            return empty;

        return await UpdateSubtaskAsync(cardId, userId, subtaskId, (db, subtask) =>
        {
            if (subtask.Text == text)
                return;

            Log(db, cardId, userId, new SubtaskEditedActivity { OldSubtaskText = subtask.Text, SubtaskText = text });
            subtask.Text = text;
        });
    }

    public Task<OperationResult> SetSubtaskCompletedAsync(int cardId, int userId, int subtaskId, bool completed) =>
        UpdateSubtaskAsync(cardId, userId, subtaskId, (db, subtask) =>
        {
            if (subtask.Completed == completed)
                return;

            subtask.Completed = completed;
            Log(db, cardId, userId, new SubtaskCompletionChangedActivity { SubtaskText = subtask.Text, Completed = completed });
        });

    public Task<OperationResult> DeleteSubtaskAsync(int cardId, int userId, int subtaskId) =>
        UpdateSubtaskAsync(cardId, userId, subtaskId, (db, subtask) =>
        {
            db.Subtasks.Remove(subtask);
            subtask.Card.Subtasks.Where(x => x != subtask).ToList().FixIndices();
            Log(db, cardId, userId, new SubtaskDeletedActivity { SubtaskText = subtask.Text });
        });

    /// <summary>Puts the subtask before another one of the card's subtasks, or last. Not written to the activity log.</summary>
    public Task<OperationResult> MoveSubtaskAsync(int cardId, int userId, int subtaskId, int? beforeSubtaskId) =>
        UpdateSubtaskAsync(cardId, userId, subtaskId, (_, subtask) =>
        {
            var subtasks = subtask.Card.Subtasks;
            subtasks.MoveBefore(subtask, subtasks.FirstOrDefault(x => x.Id == beforeSubtaskId));
        });

    /// <summary>Assigns a member of the board to the subtask, or unassigns them.</summary>
    /// <returns>The subtask's assignees after the change.</returns>
    public async Task<OperationResult<List<User>>> SetSubtaskAssigneeAsync(int cardId, int userId, int subtaskId, int memberId, bool assigned)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks
            .AccessibleBy(userId)
            .Include(x => x.Assignees)
            .Include(x => x.Card.Column)
            .FirstOrDefaultAsync(x => x.Id == subtaskId && x.CardId == cardId);

        if (subtask is null)
            return OperationResult.NotFound(SUBTASK_NOT_FOUND);

        return await SetAssigneeAsync(
            db, subtask, subtask.Card.Column.BoardId, cardId, userId, memberId, assigned,
            user => new SubtaskAssigneeAddedActivity { AssigneeId = user.Id, AssigneeName = user.DisplayName, SubtaskText = subtask.Text },
            user => new SubtaskAssigneeRemovedActivity { AssigneeId = user.Id, AssigneeName = user.DisplayName, SubtaskText = subtask.Text }
        );
    }

    /// <summary>Loads a subtask of the card with its card's subtasks, applies the change and saves.</summary>
    private async Task<OperationResult> UpdateSubtaskAsync(int cardId, int userId, int subtaskId, Action<DataContext, Subtask> change)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks
            .AccessibleBy(userId)
            .Include(x => x.Card.Subtasks)
            .FirstOrDefaultAsync(x => x.Id == subtaskId && x.CardId == cardId);

        if (subtask is null)
            return OperationResult.NotFound(SUBTASK_NOT_FOUND);

        change(db, subtask);

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    #endregion

    #region Reminders

    /// <summary>Adds a reminder; when it's due, the card's assignees and creator get an e-mail.</summary>
    public async Task<OperationResult<Reminder>> AddReminderAsync(int cardId, int userId, DateTime at)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var reminder = new Reminder { CardId = cardId, At = at };

        db.Reminders.Add(reminder);
        Log(db, cardId, userId, new ReminderAddedActivity { At = at });

        await db.SaveChangesAsync();
        return reminder;
    }

    public async Task<OperationResult> RemoveReminderAsync(int cardId, int userId, int reminderId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var reminder = await db.Reminders.AccessibleBy(userId).FirstOrDefaultAsync(x => x.Id == reminderId && x.CardId == cardId);

        if (reminder is null)
            return OperationResult.NotFound("The reminder doesn't exist, or you can't access it.");

        db.Reminders.Remove(reminder);
        Log(db, cardId, userId, new ReminderRemovedActivity { At = reminder.At });

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    #endregion

    #region Attachments

    /// <summary>Records a file that has already been saved to the uploads folder (see <see cref="AttachmentService"/>).</summary>
    public async Task<OperationResult<Attachment>> AddAttachmentAsync(int cardId, int userId, string fileName, string originalName)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var attachment = new Attachment
        {
            CardId = cardId,
            FileName = fileName,
            OriginalName = originalName,
        };

        db.Attachments.Add(attachment);
        Log(db, cardId, userId, new AttachmentUploadedActivity { FileName = originalName });

        await db.SaveChangesAsync();
        return attachment;
    }

    /// <summary>Removes the attachment. The file itself is deleted by the cleanup job.</summary>
    public async Task<OperationResult> RemoveAttachmentAsync(int cardId, int userId, int attachmentId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var attachment = await db.Attachments.AccessibleBy(userId).FirstOrDefaultAsync(x => x.Id == attachmentId && x.CardId == cardId);

        if (attachment is null)
            return OperationResult.NotFound("The attachment doesn't exist, or you can't access it.");

        db.Attachments.Remove(attachment);
        Log(db, cardId, userId, new AttachmentDeletedActivity { FileName = attachment.OriginalName });

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    #endregion

    #region Time tracking

    private const string TIME_RECORD_NOT_FOUND = "The time record doesn't exist, or you can't access it.";
    /// <summary>The longest a time record may be. A record ends now, so a longer one would start before the dates the database holds.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(365);

    private static OperationResult? ValidateDuration(TimeSpan duration) =>
        duration <= TimeSpan.Zero ? OperationResult.Invalid("Duration", "The time spent must be longer than zero.")
        : duration > MaxDuration ? OperationResult.Invalid("Duration", "The time spent can't be more than a year.")
        : null;

    /// <summary>Starts the user's timer on the card. If it's already running, that timer is returned.</summary>
    public async Task<OperationResult<TimeRecord>> StartTimerAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var running = await db.TimeRecords.FirstOrDefaultAsync(x => x.CardId == cardId && x.UserId == userId && x.EndedAt == null);

        if (running is not null)
            return running;

        var record = new TimeRecord
        {
            UserId = userId,
            CardId = cardId,
            StartedAt = DateTime.Now,
        };

        db.TimeRecords.Add(record);

        await db.SaveChangesAsync();
        return record;
    }

    /// <summary>Stops the user's running timer on the card. A record shorter than a second is discarded.</summary>
    public async Task<OperationResult> StopTimerAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // No access check: only the caller's own running timer is touched, and someone removed from the board must
        // still be able to stop the timer they left running there.
        var record = await db.TimeRecords.FirstOrDefaultAsync(x => x.CardId == cardId && x.UserId == userId && x.EndedAt == null);

        if (record is null)
            return OperationResult.Conflict("You have no timer running on this card.");

        record.EndedAt = DateTime.Now;

        if ((record.EndedAt.Value - record.StartedAt).TotalSeconds < 1)
            db.TimeRecords.Remove(record);

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    /// <summary>Adds a finished time record of the given length for the user, ending now.</summary>
    public async Task<OperationResult<TimeRecord>> AddTimeRecordAsync(int cardId, int userId, TimeSpan duration)
    {
        if (ValidateDuration(duration) is { } invalid)
            return invalid;

        using var db = _dbContextFactory.CreateDbContext();

        if (await CheckCardAsync(db, cardId, userId) is { } missing)
            return missing;

        var now = DateTime.Now;
        var record = new TimeRecord
        {
            CardId = cardId,
            UserId = userId,
            StartedAt = now - duration,
            EndedAt = now,
        };

        db.TimeRecords.Add(record);
        Log(db, cardId, userId, new TimeRecordAddedActivity { Duration = duration });

        await db.SaveChangesAsync();
        return record;
    }

    /// <summary>Changes a finished time record's length. The record is moved to end now.</summary>
    public async Task<OperationResult<TimeRecord>> EditTimeRecordAsync(int cardId, int userId, int timeRecordId, TimeSpan duration)
    {
        if (ValidateDuration(duration) is { } invalid)
            return invalid;

        using var db = _dbContextFactory.CreateDbContext();
        var record = await db.TimeRecords.AccessibleBy(userId).FirstOrDefaultAsync(x => x.Id == timeRecordId && x.CardId == cardId);

        if (record is null)
            return OperationResult.NotFound(TIME_RECORD_NOT_FOUND);

        if (record.EndedAt is null)
            return OperationResult.Conflict("The timer is still running. Stop it before changing its time.");

        var oldDuration = record.GetTotalTime();
        var now = DateTime.Now;

        record.EndedAt = now;
        record.StartedAt = now - duration;

        Log(db, cardId, userId, new TimeRecordChangedActivity { OldDuration = oldDuration, Duration = duration });

        await db.SaveChangesAsync();
        return record;
    }

    public async Task<OperationResult> RemoveTimeRecordAsync(int cardId, int userId, int timeRecordId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var record = await db.TimeRecords.AccessibleBy(userId).FirstOrDefaultAsync(x => x.Id == timeRecordId && x.CardId == cardId);

        if (record is null)
            return OperationResult.NotFound(TIME_RECORD_NOT_FOUND);

        db.TimeRecords.Remove(record);
        Log(db, cardId, userId, new TimeRecordDeletedActivity { Duration = record.GetTotalTime() });

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    #endregion

    #region Linked cards

    /// <summary>
    /// Links two cards in both directions, e.g. "is blocked by" on one side and "blocks" on the other. The user must be
    /// able to open both cards; they may be on different boards.
    /// </summary>
    /// <returns>The link as seen from <paramref name="cardId"/>.</returns>
    public async Task<OperationResult<CardLink>> LinkCardsAsync(int cardId, int userId, int targetCardId, string category)
    {
        var oppositeCategory = GetOppositeLinkCategory(category);

        if (oppositeCategory is null)
            return OperationResult.Invalid(nameof(CardLink.Category), "Unknown relationship type.");

        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AccessibleBy(userId).AsNoTracking().Include(x => x.Column.Board).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        if (cardId == targetCardId)
            return OperationResult.Invalid("TargetCardId", "You cannot link this card to itself.");

        var targetCard = await db.Cards.AccessibleBy(userId).AsNoTracking().Include(x => x.Column.Board).FirstOrDefaultAsync(x => x.Id == targetCardId);

        if (targetCard is null)
            return OperationResult.Invalid("TargetCardId", "The target card doesn't exist, or you can't access it.");

        if (await db.CardLinks.AnyAsync(x => x.CardOneId == cardId && x.CardTwoId == targetCardId))
            return OperationResult.Conflict("This card is already linked to the target card.");

        var link = new CardLink { CardOneId = cardId, CardTwoId = targetCardId, Category = category };
        db.CardLinks.Add(link);
        db.CardLinks.Add(new CardLink { CardOneId = targetCardId, CardTwoId = cardId, Category = oppositeCategory });

        Log(db, cardId, userId, Link(new LinkAddedActivity(), targetCard, category));
        Log(db, targetCardId, userId, Link(new LinkAddedActivity(), card, oppositeCategory));

        await db.SaveChangesAsync();
        return link;
    }

    /// <summary>Removes one of the card's links and its counterpart on the other card.</summary>
    public async Task<OperationResult> RemoveLinkAsync(int cardId, int userId, int linkId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.CardLinks
            .AccessibleBy(userId)
            .Include(x => x.CardOne.Column.Board)
            .Include(x => x.CardTwo.Column.Board)
            .FirstOrDefaultAsync(x => x.Id == linkId && x.CardOneId == cardId);

        if (link is null)
            return OperationResult.NotFound("The link doesn't exist, or you can't access it.");

        var reverseLink = await db.CardLinks.FirstOrDefaultAsync(x =>
            x.CardOneId == link.CardTwoId && x.CardTwoId == link.CardOneId
        );

        db.CardLinks.Remove(link);
        Log(db, link.CardOneId, userId, Link(new LinkRemovedActivity(), link.CardTwo, link.Category));

        if (reverseLink is not null)
        {
            db.CardLinks.Remove(reverseLink);
            Log(db, link.CardTwoId, userId, Link(new LinkRemovedActivity(), link.CardOne, reverseLink.Category));
        }

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    private static string? GetOppositeLinkCategory(string category)
    {
        if (Constants.LINK_TYPE_PAIRS.TryGetValue(category, out var opposite))
            return opposite;

        var reverse = Constants.LINK_TYPE_PAIRS.FirstOrDefault(x => x.Value == category);
        return reverse.Key;
    }

    private static LinkActivity Link(LinkActivity activity, Card linkedCard, string category)
    {
        activity.RelatedCardId = linkedCard.Id;
        activity.RelatedCardCode = $"{linkedCard.Column.Board.Code}-{linkedCard.Number}";
        activity.Category = category;
        return activity;
    }

    #endregion

    /// <summary>Null if the user can open the card, otherwise the not-found result to return.</summary>
    private static async Task<OperationResult?> CheckCardAsync(DataContext db, int cardId, int userId) =>
        await db.Cards.AccessibleBy(userId).AnyAsync(x => x.Id == cardId) ? null : OperationResult.NotFound(CARD_NOT_FOUND);

    /// <summary>Changes a field on the card and logs it. The activity is created before the change, so it can see the old value.</summary>
    /// <param name="activity">The log entry, or null for changes that aren't logged.</param>
    /// <param name="unchanged">
    /// Whether the change would leave the card as it is. Then nothing is saved or logged, so the activity log only
    /// shows real changes.
    /// </param>
    private async Task<OperationResult> UpdateAsync(
        int cardId,
        int userId,
        Func<Card, Activity>? activity,
        Action<Card> change,
        Func<Card, bool> unchanged
    )
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AccessibleBy(userId).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return OperationResult.NotFound(CARD_NOT_FOUND);

        if (unchanged(card))
            return OperationResult.Success;

        var entry = activity?.Invoke(card);
        change(card);

        if (entry is not null)
            Log(db, cardId, userId, entry);

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    private static void Log(DataContext db, int cardId, int userId, Activity activity)
    {
        activity.CardId = cardId;
        activity.UserId = userId;
        db.Activities.Add(activity);
    }

    private static CardMovedActivity Moved(Column from, Column to) =>
        new()
        {
            FromColumnId = from.Id,
            FromColumnName = from.Name,
            ToColumnId = to.Id,
            ToColumnName = to.Name,
            ToColumnFinished = to.Finished,
        };
}
