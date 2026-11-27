using Ticky.Base.Models;

namespace Ticky.Internal.Helpers;

/// <summary>
/// The card lists people see, as query conditions shared by the web UI and the API: a board's filter, search, and
/// Your work. They don't check access; start from <c>db.Cards.AccessibleBy(userId)</c> or a board the user can open.
/// </summary>
public static class CardQueryHelper
{
    /// <summary>The cards that pass a board's filter, as set in the filter dialog.</summary>
    public static IQueryable<Card> Matching(this IQueryable<Card> cards, FilterCardsModel filter)
    {
        if (filter.ExcludeCompleted)
            cards = cards.Where(x => !x.Column.Finished);

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            cards = cards.Where(x =>
                x.Name.Contains(text)
                || (x.Column.Board.Code + "-" + x.Number).Contains(text)
                || (x.Description != null && x.Description.Contains(text))
            );
        }

        var assigneeIds = filter.AssignedUserIds;

        if (assigneeIds.Count > 0 || filter.IncludeUnassigned)
            cards = cards.Where(card =>
                (assigneeIds.Count > 0 && card.Assignees.Any(a => assigneeIds.Contains(a.Id)))
                || (filter.IncludeUnassigned && !card.Assignees.Any())
            );

        var labelIds = filter.LabelIds;

        if (labelIds.Count > 0)
            cards = cards.Where(card => card.Labels.Any(x => labelIds.Contains(x.Id)));

        var excludedLabelIds = filter.ExcludedLabelIds;

        if (excludedLabelIds.Count > 0)
            cards = cards.Where(card => !card.Labels.Any(x => excludedLabelIds.Contains(x.Id)));

        return cards;
    }

    /// <summary>
    /// Cards whose name or key (like <c>TKY-12</c>) contains the text, the card with exactly that key first and then
    /// the newest.
    /// </summary>
    public static IQueryable<Card> Search(this IQueryable<Card> cards, string text)
    {
        text = text.Trim();

        return cards
            .Where(x => x.Name.Contains(text) || (x.Column.Board.Code + "-" + x.Number).Contains(text))
            .OrderByDescending(x => x.Column.Board.Code + "-" + x.Number == text)
            .ThenByDescending(x => x.Id);
    }

    /// <summary>
    /// Your work: open cards assigned to the user, without snoozed ones (hidden as on the board). Those with the
    /// soonest deadline come first, then the highest priority.
    /// </summary>
    public static IQueryable<Card> WorkOf(this IQueryable<Card> cards, int userId) =>
        cards
            .Where(x => x.Assignees.Any(a => a.Id == userId) && !x.Column.Finished && !x.SnoozedUntil.HasValue)
            .OrderBy(x => x.Deadline == null)
            .ThenBy(x => x.Deadline)
            .ThenByDescending(x => x.Priority);

    /// <summary>The user's timers that are still running, the longest-running first.</summary>
    public static IQueryable<TimeRecord> RunningOf(this IQueryable<TimeRecord> records, int userId) =>
        records.Where(x => x.UserId == userId && x.EndedAt == null).OrderBy(x => x.StartedAt);
}
