namespace Ticky.Internal.Services;

/// <summary>
/// Counts the guesses made against each code. The <c>Codes</c> table has no column for it and this fix avoids a
/// migration, so the counts live in memory (register it as a singleton). A code only lives for minutes and each one
/// is a fresh random value, so a restart handing out a few more guesses on the codes in flight doesn't matter, and
/// nothing here is shared with the sign-in lockout counters of the user.
/// </summary>
public class CodeAttemptTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<int, (int Count, DateTime Since)> _attempts = [];

    /// <summary>
    /// Registers one more guess against the code and returns how many have been made, this one included. The count is
    /// taken before the guess is compared, so guesses made in parallel can't exceed the limit.
    /// </summary>
    /// <param name="codeId">Id of the code that is being guessed.</param>
    /// <param name="retention">How long a count is kept. Entries older than any code can live are dropped.</param>
    public int Register(int codeId, TimeSpan retention)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            foreach (
                var stale in _attempts
                    .Where(x => now - x.Value.Since > retention)
                    .Select(x => x.Key)
                    .ToList()
            )
                _attempts.Remove(stale);

            var count = 1;
            var since = now;

            if (_attempts.TryGetValue(codeId, out var entry))
            {
                count = entry.Count + 1;
                since = entry.Since;
            }

            _attempts[codeId] = (count, since);
            return count;
        }
    }

    /// <summary>Drops the count of a code that was used up or deleted.</summary>
    public void Forget(int codeId)
    {
        lock (_lock)
            _attempts.Remove(codeId);
    }
}
