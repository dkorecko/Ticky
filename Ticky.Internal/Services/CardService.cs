using System.Net;
using Ticky.Base.Converters;
using Ticky.Base.Entities.Owned;
using Ticky.Internal.Helpers;

namespace Ticky.Internal.Services;

/// <summary>
/// Card edits made from the card detail view and its dialogs. Most of them also write an entry to the card's activity log.
/// Each method loads only what it changes and does nothing if the card or item no longer exists.
/// </summary>
/// <remarks>
/// Activity text is rendered as HTML, so every user-entered value placed in it must go through <see cref="Html"/>.
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
        UpdateAsync(cardId, userId, "<b>changed</b> the title", card => card.Name = name);

    public Task UpdateDescriptionAsync(int cardId, int userId, string description) =>
        UpdateAsync(cardId, userId, "<b>changed</b> the description", card => card.Description = description);

    public async Task SetPriorityAsync(int cardId, int userId, CardPriority priority)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null || card.Priority == priority)
            return;

        card.Priority = priority;
        AddActivity(db, cardId, userId, $"<b>changed</b> the priority to <b>{priority}</b>");

        await db.SaveChangesAsync();
    }

    public Task SetDeadlineAsync(int cardId, int userId, DateTime deadline) =>
        UpdateAsync(
            cardId,
            userId,
            $"<b>set</b> the deadline for <b>{deadline.ToReadableStringWithTime()}</b>",
            card =>
            {
                card.Deadline = deadline;
                card.DeadlineProcessed = false;
            }
        );

    public Task RemoveDeadlineAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, "<b>deleted</b> the deadline", card => card.Deadline = null);

    public Task SnoozeAsync(int cardId, int userId, DateTime until) =>
        UpdateAsync(
            cardId,
            userId,
            $"<b>snoozed</b> this card until <b>{until.ToReadableStringWithTime()}</b>",
            card => card.SnoozedUntil = until
        );

    public Task UnsnoozeAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, "<b>unsnoozed</b> this card", card => card.SnoozedUntil = null);

    public Task SetRepeatAsync(int cardId, int userId, RepeatInfo repeatInfo) =>
        UpdateAsync(
            cardId,
            userId,
            $"<b>set up card repeat</b> for this card for <b>{repeatInfo.Type.GetDisplayName()}</b>",
            card => card.RepeatInfo = repeatInfo
        );

    public Task RemoveRepeatAsync(int cardId, int userId) =>
        UpdateAsync(cardId, userId, "<b>turned repeating off</b> for this card", card => card.RepeatInfo = null);

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

        card.Column = targetColumn;
        AddActivity(db, cardId, userId, $"<b>moved</b> the card to <b>{Html(targetColumn.Name)}</b>");

        await db.SaveChangesAsync();
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
            AddActivity(db, cardId, userId, $"<b>removed</b> label <b>{Html(label.Name)}</b>");
        }
        else
        {
            card.Labels.Add(label);
            AddActivity(db, cardId, userId, $"<b>added</b> label <b>{Html(label.Name)}</b>");
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
        AddActivity(db, cardId, userId, "<b>joined</b> the card");

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
        AddActivity(db, cardId, userId, "<b>left</b> the card");

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
            AddActivity(db, cardId, userId, $"<b>removed</b> the user <b>{Html(member.DisplayName)}</b> from the assignees");
        }
        else
        {
            card.Assignees.Add(member);
            AddActivity(db, cardId, userId, $"<b>added</b> the user <b>{Html(member.DisplayName)}</b> as an assignee");
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
        AddActivity(db, cardId, userId, "<b>posted</b> a comment");

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
        AddActivity(db, cardId, userId, $"<b>added</b> subtask <b>{Html(text)}</b>");

        await db.SaveChangesAsync();
    }

    public async Task EditSubtaskAsync(int subtaskId, int userId, string text)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
            return;

        AddActivity(db, subtask.CardId, userId, $"<b>edited</b> subtask <b>{Html(subtask.Text)}</b> to <b>{Html(text)}</b>");
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
        AddActivity(
            db,
            subtask.CardId,
            userId,
            $"<b>marked</b> the subtask <b>{Html(subtask.Text)}</b> as <b>{(completed ? "completed" : "to be done")}</b>"
        );

        await db.SaveChangesAsync();
    }

    public async Task DeleteSubtaskAsync(int subtaskId, int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks.FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
            return;

        db.Subtasks.Remove(subtask);
        AddActivity(db, subtask.CardId, userId, $"<b>deleted</b> subtask <b>{Html(subtask.Text)}</b>");

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
            AddActivity(db, subtask.CardId, userId, $"<b>removed</b> the user <b>{Html(member.DisplayName)}</b> from the subtask assignees");
        }
        else
        {
            subtask.Assignees.Add(member);
            AddActivity(db, subtask.CardId, userId, $"<b>added</b> the user <b>{Html(member.DisplayName)}</b> as a subtask assignee");
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
        AddActivity(db, cardId, userId, $"<b>added</b> a reminder for <b>{at.ToReadableStringWithTime()}</b>");

        await db.SaveChangesAsync();
    }

    public async Task RemoveReminderAsync(int cardId, int userId, int reminderId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var reminder = await db.Reminders.FirstOrDefaultAsync(x => x.Id == reminderId && x.CardId == cardId);

        if (reminder is null)
            return;

        db.Reminders.Remove(reminder);
        AddActivity(db, cardId, userId, $"<b>deleted</b> the reminder at <b>{reminder.At.ToReadableStringWithTime()}</b>");

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
        AddActivity(db, cardId, userId, $"<b>uploaded</b> file named <b>{Html(originalName)}</b>");

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
        AddActivity(db, cardId, userId, $"<b>deleted</b> file named <b>{Html(attachment.OriginalName)}</b>");

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
        AddActivity(db, cardId, userId, $"<b>added</b> a time record for <b>{duration.ToElapsedString()}</b>");

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

        AddActivity(
            db,
            record.CardId,
            userId,
            $"<b>changed</b> the time record from <b>{oldDuration.ConvertToString()}</b> to <b>{duration.ConvertToString()}</b>"
        );

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
        AddActivity(db, cardId, userId, $"<b>deleted</b> time record of <b>{duration.ToElapsedString()}</b>");

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

        AddActivity(db, cardId, userId, LinkActivityText("added", targetCard, category));
        AddActivity(db, targetCardId, userId, LinkActivityText("added", card, oppositeCategory));

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
        AddActivity(db, link.CardOneId, userId, LinkActivityText("deleted", link.CardTwo, link.Category));

        if (reverseLink is not null)
        {
            db.CardLinks.Remove(reverseLink);
            AddActivity(db, link.CardTwoId, userId, LinkActivityText("deleted", link.CardOne, reverseLink.Category));
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

    private static string LinkActivityText(string verb, Card linkedCard, string category) =>
        $"<b>{verb}</b> a linked issue <b>{Html(linkedCard.Column.Board.Code)}-{linkedCard.Number}</b> with <b>{Html(category)}</b> relationship";

    #endregion

    private async Task UpdateAsync(int cardId, int userId, string activityText, Action<Card> change)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
            return;

        change(card);
        AddActivity(db, cardId, userId, activityText);

        await db.SaveChangesAsync();
    }

    private static void AddActivity(DataContext db, int cardId, int userId, string text) =>
        db.Activities.Add(new Activity
        {
            Text = text,
            UserId = userId,
            CardId = cardId,
        });

    /// <summary>Escapes a user-entered value for use inside activity text.</summary>
    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
