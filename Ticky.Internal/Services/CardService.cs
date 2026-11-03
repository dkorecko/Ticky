using Ticky.Base.Converters;
using Ticky.Base.Entities.Owned;
using Ticky.Internal.Helpers;

namespace Ticky.Internal.Services;

/// <summary>
/// Card edits made from the card detail view and its dialogs. Most of them also write an entry to the card's activity log.
/// Each method loads only what it changes and does nothing if the card or item no longer exists.
/// </summary>
/// <remarks>
/// Activities store what happened as data; the text is built when shown (see <see cref="ActivityFormatter"/>).
/// </remarks>
public class CardService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public CardService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Whether the user may see and edit the card: they must be a member of its board or of the board's project
    /// (the same rule as <see cref="Board.VerifyAccess"/>). False if the card does not exist.
    /// </summary>
    public async Task<bool> CanAccessCardAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return await db.Cards
            .Where(x => x.Id == cardId)
            .AnyAsync(x =>
                x.Column.Board.Memberships.Any(m => m.UserId == userId)
                || x.Column.Board.Project.Memberships.Any(m => m.UserId == userId)
            );
    }

    #region Card fields

    public Task RenameAsync(int cardId, int userId, string name) =>
        UpdateAsync(cardId, userId, card => new TitleChangedActivity { OldTitle = card.Name, NewTitle = name }, card => card.Name = name);

    public Task UpdateDescriptionAsync(int cardId, int userId, string description) =>
        UpdateAsync(cardId, userId, _ => new DescriptionChangedActivity(), card => card.Description = description);

    public async Task SetPriorityAsync(int cardId, int userId, CardPriority priority)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null || card.Priority == priority)
            return;

        card.Priority = priority;
        Log(db, cardId, userId, new PriorityChangedActivity { Priority = priority });

        await db.SaveChangesAsync();
    }

    public Task SetDeadlineAsync(int cardId, int userId, DateTime deadline) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new DeadlineSetActivity { Deadline = deadline },
            card =>
            {
                card.Deadline = deadline;
                card.DeadlineProcessed = false;
            }
        );

    public Task RemoveDeadlineAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, _ => new DeadlineRemovedActivity(), card => card.Deadline = null);

    public Task SnoozeAsync(int cardId, int userId, DateTime until) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new SnoozedActivity { Until = until },
            card => card.SnoozedUntil = until
        );

    public Task UnsnoozeAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, _ => new UnsnoozedActivity(), card => card.SnoozedUntil = null);

    public Task SetRepeatAsync(int cardId, int userId, RepeatInfo repeatInfo) =>
        UpdateAsync(
            cardId,
            userId,
            _ => new RepeatSetActivity { RepeatType = repeatInfo.Type },
            card => card.RepeatInfo = repeatInfo
        );

    public Task RemoveRepeatAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, _ => new RepeatDisabledActivity(), card => card.RepeatInfo = null);

    /// <summary>Moves the card to the column next to its current one. Use -1 for the previous column, +1 for the next.</summary>
    public async Task MoveToAdjacentColumnAsync(int cardId, int userId, int offset)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Column).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return;

        var targetColumn = await db.Columns.FirstOrDefaultAsync(x =>
            x.BoardId == card.Column.BoardId && x.Index == card.Column.Index + offset
        );

        if (targetColumn is null)
            return;

        Log(db, cardId, userId, Moved(card.Column, targetColumn));
        card.Column = targetColumn;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Moves the card to another column on the same board, placing it before <paramref name="beforeCardId"/> or at the end.
    /// </summary>
    /// <returns>False if the card or column no longer exists, or the column is full.</returns>
    public async Task<bool> MoveToColumnAsync(int cardId, int userId, int targetColumnId, int? beforeCardId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Column.Cards).FirstOrDefaultAsync(x => x.Id == cardId);
        var targetColumn = await db.Columns.Include(x => x.Cards).FirstOrDefaultAsync(x => x.Id == targetColumnId);

        if (card is null || targetColumn is null || targetColumn.BoardId != card.Column.BoardId)
            return false;

        if (targetColumn.Id == card.ColumnId)
            return true;

        if (targetColumn.MaxCards != 0 && targetColumn.Cards.Count + 1 > targetColumn.MaxCards)
            return false;

        var oldColumn = card.Column;
        oldColumn.Cards.Remove(card);
        oldColumn.Cards.FixIndices();

        var targetIndex = targetColumn.Cards.Count != 0 ? targetColumn.Cards.Max(x => x.Index) + 1 : 0;
        var beforeCard = targetColumn.Cards.FirstOrDefault(x => x.Id == beforeCardId);

        if (beforeCard is not null)
        {
            targetIndex = beforeCard.Index;

            foreach (var other in targetColumn.Cards.Where(x => x.Index >= beforeCard.Index))
                other.Index++;
        }

        Log(db, cardId, userId, Moved(oldColumn, targetColumn));
        targetColumn.Cards.Add(card);
        card.Column = targetColumn;
        card.Index = targetIndex;
        targetColumn.Cards.FixIndices();

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Adds the label to the card, or removes it if the card already has it.</summary>
    /// <returns>The card's labels after the change, or null if the card or label no longer exists.</returns>
    public async Task<List<Label>?> ToggleLabelAsync(int cardId, int userId, int labelId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Labels).FirstOrDefaultAsync(x => x.Id == cardId);
        var label = await db.Labels.FirstOrDefaultAsync(x => x.Id == labelId);

        if (card is null || label is null)
            return null;

        if (card.Labels.Remove(label))
        {
            Log(db, cardId, userId, new LabelRemovedActivity { LabelId = label.Id, LabelName = label.Name });
        }
        else
        {
            card.Labels.Add(label);
            Log(db, cardId, userId, new LabelAddedActivity { LabelId = label.Id, LabelName = label.Name });
        }

        await db.SaveChangesAsync();
        return card.Labels;
    }

    #endregion

    #region Assignees

    public async Task JoinAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Assignees).FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null || card.Assignees.Any(x => x.Id == userId))
            return;

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return;

        card.Assignees.Add(user);
        Log(db, cardId, userId, new AssigneeAddedActivity { AssigneeId = user.Id, AssigneeName = user.DisplayName });

        await db.SaveChangesAsync();
    }

    public async Task LeaveAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Assignees).FirstOrDefaultAsync(x => x.Id == cardId);
        var assignee = card?.Assignees.FirstOrDefault(x => x.Id == userId);

        if (card is null || assignee is null)
            return;

        card.Assignees.Remove(assignee);
        Log(db, cardId, userId, new AssigneeRemovedActivity { AssigneeId = assignee.Id, AssigneeName = assignee.DisplayName });

        await db.SaveChangesAsync();
    }

    /// <summary>Assigns the member to the card, or unassigns them if they already are.</summary>
    /// <returns>The card's assignees after the change, or null if the card or member no longer exists.</returns>
    public async Task<List<User>?> ToggleAssigneeAsync(int cardId, int userId, int memberId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Assignees).FirstOrDefaultAsync(x => x.Id == cardId);
        var member = await db.Users.FirstOrDefaultAsync(x => x.Id == memberId);

        if (card is null || member is null)
            return null;

        if (card.Assignees.Remove(member))
        {
            Log(db, cardId, userId, new AssigneeRemovedActivity { AssigneeId = member.Id, AssigneeName = member.DisplayName });
        }
        else
        {
            card.Assignees.Add(member);
            Log(db, cardId, userId, new AssigneeAddedActivity { AssigneeId = member.Id, AssigneeName = member.DisplayName });
        }

        await db.SaveChangesAsync();
        return card.Assignees;
    }

    #endregion

    #region Comments

    public async Task AddCommentAsync(int cardId, int userId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return;

        db.Comments.Add(new Comment
        {
            Text = text,
            CreatedById = userId,
            CardId = cardId,
        });
        Log(db, cardId, userId, new CommentPostedActivity());

        await db.SaveChangesAsync();
    }

    #endregion

    #region Subtasks

    public async Task AddSubtaskAsync(int cardId, int userId, string text)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return;

        var subtasks = await db.Subtasks.Where(x => x.CardId == cardId).ToListAsync();

        db.Subtasks.Add(new Subtask
        {
            CardId = cardId,
            Text = text,
            Index = subtasks.GetNextIndex(),
        });
        Log(db, cardId, userId, new SubtaskAddedActivity { SubtaskText = text });

        await db.SaveChangesAsync();
    }

    public async Task EditSubtaskAsync(int subtaskId, int userId, string text)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
            return;

        Log(db, subtask.CardId, userId, new SubtaskEditedActivity { OldSubtaskText = subtask.Text, SubtaskText = text });
        subtask.Text = text;

        await db.SaveChangesAsync();
    }

    public async Task SetSubtaskCompletedAsync(int subtaskId, int userId, bool completed)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
            return;

        subtask.Completed = completed;
        Log(db, subtask.CardId, userId, new SubtaskCompletionChangedActivity { SubtaskText = subtask.Text, Completed = completed });

        await db.SaveChangesAsync();
    }

    public async Task DeleteSubtaskAsync(int subtaskId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
            return;

        db.Subtasks.Remove(subtask);
        Log(db, subtask.CardId, userId, new SubtaskDeletedActivity { SubtaskText = subtask.Text });

        var remaining = await db.Subtasks.Where(x => x.CardId == subtask.CardId && x.Id != subtaskId).ToListAsync();
        remaining.FixIndices();

        await db.SaveChangesAsync();
    }

    /// <summary>Assigns the member to the subtask, or unassigns them if they already are.</summary>
    /// <returns>The subtask's assignees after the change, or null if the subtask or member no longer exists.</returns>
    public async Task<List<User>?> ToggleSubtaskAssigneeAsync(int subtaskId, int userId, int memberId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.Include(x => x.Assignees).FirstOrDefaultAsync(x => x.Id == subtaskId);
        var member = await db.Users.FirstOrDefaultAsync(x => x.Id == memberId);

        if (subtask is null || member is null)
            return null;

        if (subtask.Assignees.Remove(member))
        {
            Log(db, subtask.CardId, userId, new SubtaskAssigneeRemovedActivity { AssigneeId = member.Id, AssigneeName = member.DisplayName, SubtaskText = subtask.Text });
        }
        else
        {
            subtask.Assignees.Add(member);
            Log(db, subtask.CardId, userId, new SubtaskAssigneeAddedActivity { AssigneeId = member.Id, AssigneeName = member.DisplayName, SubtaskText = subtask.Text });
        }

        await db.SaveChangesAsync();
        return subtask.Assignees;
    }

    #endregion

    #region Reminders

    public async Task AddReminderAsync(int cardId, int userId, DateTime at)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return;

        db.Reminders.Add(new Reminder { CardId = cardId, At = at });
        Log(db, cardId, userId, new ReminderAddedActivity { At = at });

        await db.SaveChangesAsync();
    }

    public async Task RemoveReminderAsync(int cardId, int userId, int reminderId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var reminder = await db.Reminders.FirstOrDefaultAsync(x => x.Id == reminderId && x.CardId == cardId);

        if (reminder is null)
            return;

        db.Reminders.Remove(reminder);
        Log(db, cardId, userId, new ReminderRemovedActivity { At = reminder.At });

        await db.SaveChangesAsync();
    }

    #endregion

    #region Attachments

    /// <summary>Records a file that has already been saved to the uploads folder.</summary>
    /// <returns>False if the card no longer exists.</returns>
    public async Task<bool> AddAttachmentAsync(int cardId, int userId, string fileName, string originalName)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return false;

        db.Attachments.Add(new Attachment
        {
            CardId = cardId,
            FileName = fileName,
            OriginalName = originalName,
        });
        Log(db, cardId, userId, new AttachmentUploadedActivity { FileName = originalName });

        await db.SaveChangesAsync();
        return true;
    }

    public async Task RemoveAttachmentAsync(int cardId, int userId, int attachmentId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var attachment = await db.Attachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.CardId == cardId);

        if (attachment is null)
            return;

        db.Attachments.Remove(attachment);
        Log(db, cardId, userId, new AttachmentDeletedActivity { FileName = attachment.OriginalName });

        await db.SaveChangesAsync();
    }

    #endregion

    #region Time tracking

    public async Task StartTimerAsync(int cardId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return;

        db.TimeRecords.Add(new TimeRecord
        {
            UserId = userId,
            CardId = cardId,
            StartedAt = DateTime.Now,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Stops a running timer. Records shorter than a second are discarded.</summary>
    public async Task EndTimerAsync(int cardId, int timeRecordId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var record = await db.TimeRecords.FirstOrDefaultAsync(x => x.Id == timeRecordId && x.CardId == cardId);

        if (record is null)
            return;

        record.EndedAt = DateTime.Now;

        if ((record.EndedAt.Value - record.StartedAt).TotalSeconds < 1)
            db.TimeRecords.Remove(record);

        await db.SaveChangesAsync();
    }

    /// <summary>Adds a finished time record of the given length, ending now.</summary>
    public async Task AddTimeRecordAsync(int cardId, int userId, TimeSpan duration)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Cards.AnyAsync(x => x.Id == cardId))
            return;

        var now = DateTime.Now;

        db.TimeRecords.Add(new TimeRecord
        {
            CardId = cardId,
            UserId = userId,
            StartedAt = now - duration,
            EndedAt = now,
        });
        Log(db, cardId, userId, new TimeRecordAddedActivity { Duration = duration });

        await db.SaveChangesAsync();
    }

    /// <summary>Changes a finished time record's length. The record is moved to end now.</summary>
    public async Task EditTimeRecordAsync(int timeRecordId, int userId, TimeSpan duration)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var record = await db.TimeRecords.FirstOrDefaultAsync(x => x.Id == timeRecordId);

        if (record is null)
            return;

        var oldDuration = (record.EndedAt ?? DateTime.Now) - record.StartedAt;
        var now = DateTime.Now;

        record.EndedAt = now;
        record.StartedAt = now - duration;

        Log(db, record.CardId, userId, new TimeRecordChangedActivity { OldDuration = oldDuration, Duration = duration });

        await db.SaveChangesAsync();
    }

    public async Task RemoveTimeRecordAsync(int cardId, int userId, int timeRecordId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var record = await db.TimeRecords.FirstOrDefaultAsync(x => x.Id == timeRecordId && x.CardId == cardId);

        if (record is null)
            return;

        var duration = (record.EndedAt ?? DateTime.Now) - record.StartedAt;

        db.TimeRecords.Remove(record);
        Log(db, cardId, userId, new TimeRecordDeletedActivity { Duration = duration });

        await db.SaveChangesAsync();
    }

    #endregion

    #region Linked cards

    /// <summary>Links two cards in both directions, e.g. "is blocked by" on one side and "blocks" on the other.</summary>
    /// <returns>An error to show the user, or null on success.</returns>
    public async Task<string?> LinkCardsAsync(int cardId, int targetCardId, int userId, string category)
    {
        if (cardId == targetCardId)
            return "You cannot link this card to itself.";

        var oppositeCategory = GetOppositeLinkCategory(category);

        if (oppositeCategory is null)
            return "Unknown relationship type.";

        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.AsNoTracking().Include(x => x.Column.Board).FirstOrDefaultAsync(x => x.Id == cardId);
        var targetCard = await db.Cards.AsNoTracking().Include(x => x.Column.Board).FirstOrDefaultAsync(x => x.Id == targetCardId);

        if (card is null)
            return "This card does not exist anymore.";

        if (targetCard is null)
            return "Target card does not exist anymore.";

        if (await db.CardLinks.AnyAsync(x => x.CardOneId == cardId && x.CardTwoId == targetCardId))
            return "This card is already linked to the target card.";

        db.CardLinks.Add(new CardLink { CardOneId = cardId, CardTwoId = targetCardId, Category = category });
        db.CardLinks.Add(new CardLink { CardOneId = targetCardId, CardTwoId = cardId, Category = oppositeCategory });

        Log(db, cardId, userId, Link(new LinkAddedActivity(), targetCard, category));
        Log(db, targetCardId, userId, Link(new LinkAddedActivity(), card, oppositeCategory));

        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Removes a link and its counterpart on the other card.</summary>
    public async Task RemoveLinkAsync(int linkId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.CardLinks
            .Include(x => x.CardOne.Column.Board)
            .Include(x => x.CardTwo.Column.Board)
            .FirstOrDefaultAsync(x => x.Id == linkId);

        if (link is null)
            return;

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

    /// <summary>Changes a field on the card and logs it. The activity is created before the change, so it can see the old value.</summary>
    private async Task UpdateAsync(int cardId, int userId, Func<Card, Activity> activity, Action<Card> change)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return;

        var entry = activity(card);
        change(card);
        Log(db, cardId, userId, entry);

        await db.SaveChangesAsync();
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
