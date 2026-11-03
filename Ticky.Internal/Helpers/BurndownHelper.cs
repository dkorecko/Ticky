using System.Globalization;

namespace Ticky.Internal.Helpers;

/// <summary>
/// Rebuilds how many cards were open on each past day from the cards' move history.
/// A card is done while it sits in a column marked as finished.
/// </summary>
public static class BurndownHelper
{
    /// <param name="CreatedAt">When the card was created. It is open from then until its first move to a finished column.</param>
    /// <param name="DoneNow">Whether the card is in a finished column right now.</param>
    /// <param name="Moves">Recorded moves and whether each one went to a finished column.</param>
    /// <param name="LastActivityAt">The card's latest activity of any kind, if any.</param>
    public record CardTimeline(DateTime CreatedAt, bool DoneNow, IReadOnlyList<(DateTime At, bool Done)> Moves, DateTime? LastActivityAt);

    /// <summary>Loads the timeline of every card on the board.</summary>
    public static async Task<List<CardTimeline>> LoadTimelinesAsync(DataContext db, int boardId)
    {
        var columnFinished = await db.Columns
            .Where(x => x.BoardId == boardId)
            .ToDictionaryAsync(x => x.Id, x => x.Finished);

        var cards = await db.Cards
            .Where(x => x.Column.BoardId == boardId)
            .Select(x => new { x.Id, x.CreatedAt, x.ColumnId })
            .ToListAsync();

        var boardActivities = db.Activities.Where(x => x.Card.Column.BoardId == boardId);

        var lastActivity = await boardActivities
            .GroupBy(x => x.CardId)
            .Select(x => new { CardId = x.Key, At = x.Max(a => a.CreatedAt) })
            .ToDictionaryAsync(x => x.CardId, x => x.At);

        var moves = (await boardActivities
                .OfType<CardMovedActivity>()
                .Select(x => new { x.CardId, x.CreatedAt, x.ToColumnId, x.ToColumnFinished })
                .ToListAsync())
            .Select(x => new
            {
                x.CardId,
                x.CreatedAt,
                // Moves from before history was typed only know the target column, so use its current setting.
                Done = x.ToColumnFinished
                    ?? (x.ToColumnId is int id && columnFinished.TryGetValue(id, out var finished) ? finished : (bool?)null),
            })
            .Where(x => x.Done is not null)
            .ToLookup(x => x.CardId, x => (x.CreatedAt, x.Done!.Value));

        return cards
            .Select(card => new CardTimeline(
                card.CreatedAt,
                columnFinished.GetValueOrDefault(card.ColumnId),
                moves[card.Id].ToList(),
                lastActivity.TryGetValue(card.Id, out var at) ? at : null
            ))
            .ToList();
    }

    /// <summary>
    /// Open card count at the end of each day from <paramref name="from"/> to <paramref name="today"/>, both included.
    /// </summary>
    /// <remarks>
    /// Moves made before this history was recorded (and every drag-and-drop move made back then) are missing.
    /// When a card's recorded moves don't explain where it is now, the missing change is assumed to have happened
    /// at its latest activity, so the last day always matches the board as it is.
    /// </remarks>
    public static List<(DateTime Date, int Open)> GetOpenPerDay(IReadOnlyCollection<CardTimeline> cards, DateTime from, DateTime today)
    {
        from = from.Date;
        today = today.Date;

        if (from > today)
            return [];

        var timelines = cards.Select(GetStateChanges).ToList();

        return Enumerable
            .Range(0, (today - from).Days + 1)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                var endOfDay = day.AddDays(1);
                return (day, timelines.Count(changes => IsOpenAt(changes, endOfDay)));
            })
            .ToList();
    }

    /// <summary>
    /// SVG polyline points spreading the values across a <paramref name="width"/> × <paramref name="height"/> box,
    /// with 0 at the bottom and <paramref name="max"/> at the top. Always uses '.' decimals, as SVG requires.
    /// </summary>
    public static string ToSvgPoints(IReadOnlyList<int> values, double width, double height, int max)
    {
        if (values.Count == 0)
            return string.Empty;

        var xStep = values.Count > 1 ? width / (values.Count - 1) : 0;
        var yScale = height / Math.Max(1, max);

        return string.Join(
            " ",
            values.Select((value, i) =>
                string.Create(CultureInfo.InvariantCulture, $"{i * xStep:0.##},{height - value * yScale:0.##}")
            )
        );
    }

    private static List<(DateTime At, bool Done)> GetStateChanges(CardTimeline card)
    {
        var changes = new List<(DateTime At, bool Done)> { (card.CreatedAt, false) };
        changes.AddRange(card.Moves.Where(x => x.At >= card.CreatedAt).OrderBy(x => x.At));

        if (changes[^1].Done != card.DoneNow)
        {
            var lastKnown = card.LastActivityAt ?? card.CreatedAt;
            changes.Add((lastKnown > changes[^1].At ? lastKnown : changes[^1].At, card.DoneNow));
        }

        return changes;
    }

    private static bool IsOpenAt(List<(DateTime At, bool Done)> changes, DateTime moment)
    {
        bool? done = null;

        foreach (var change in changes)
        {
            if (change.At >= moment)
                break;

            done = change.Done;
        }

        return done == false;
    }
}
