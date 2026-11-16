namespace Ticky.Internal.Services;

/// <summary>
/// Limits how often one client can try to sign up. The sign-up form has to say when an e-mail address is already in
/// use, which lets anyone look up which addresses have an account. Every attempt that gets that far counts, and only a
/// few are allowed per window. The windows live in memory: a restart clears them and instances don't share them.
/// </summary>
public class SignUpRateLimiter
{
    public const int DEFAULT_ATTEMPTS = 5;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(10);

    private readonly int _attempts;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, (DateTimeOffset Start, int Count)> _clients = new();
    private readonly object _lock = new();
    private DateTimeOffset _lastCleanup;

    public TimeSpan Window { get; }

    /// <summary>How many clients are remembered right now. Clients whose window is over are dropped over time.</summary>
    public int TrackedClients
    {
        get
        {
            lock (_lock)
                return _clients.Count;
        }
    }

    public SignUpRateLimiter()
        : this(DEFAULT_ATTEMPTS, DefaultWindow, TimeProvider.System) { }

    public SignUpRateLimiter(int attempts, TimeSpan window, TimeProvider timeProvider)
    {
        _attempts = attempts;
        Window = window;
        _timeProvider = timeProvider;
        _lastCleanup = timeProvider.GetUtcNow();
    }

    /// <summary>Counts an attempt by the client and returns whether it may go ahead.</summary>
    public bool TryAcquire(string client)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (now - _lastCleanup >= Window)
            {
                foreach (
                    var expired in _clients
                        .Where(x => now - x.Value.Start >= Window)
                        .Select(x => x.Key)
                        .ToList()
                )
                    _clients.Remove(expired);

                _lastCleanup = now;
            }

            if (!_clients.TryGetValue(client, out var entry) || now - entry.Start >= Window)
                entry = (now, 0);

            // A refused attempt doesn't move the window, so waiting it out is enough.
            if (entry.Count >= _attempts)
            {
                _clients[client] = entry;
                return false;
            }

            _clients[client] = (entry.Start, entry.Count + 1);
            return true;
        }
    }
}
