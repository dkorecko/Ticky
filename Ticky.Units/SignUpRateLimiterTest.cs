namespace Ticky.Units;

public class SignUpRateLimiterTest
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private ManualTime _time = null!;
    private SignUpRateLimiter _limiter = null!;

    [SetUp]
    public void Setup()
    {
        _time = new ManualTime();
        _limiter = new SignUpRateLimiter(3, Window, _time);
    }

    [Test]
    public void AllowsTheConfiguredNumberOfAttempts_ThenRefuses()
    {
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);
    }

    [Test]
    public void ClientsAreCountedSeparately()
    {
        for (var i = 0; i < 3; i++)
            _limiter.TryAcquire("1.1.1.1");

        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);
        Assert.That(_limiter.TryAcquire("2.2.2.2"), Is.True);
    }

    [Test]
    public void AllowsAgainOnceTheWindowIsOver()
    {
        for (var i = 0; i < 3; i++)
            _limiter.TryAcquire("1.1.1.1");

        _time.Advance(Window - TimeSpan.FromSeconds(1));
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
    }

    [Test]
    public void RefusedAttemptsDoNotMoveTheWindow()
    {
        for (var i = 0; i < 3; i++)
            _limiter.TryAcquire("1.1.1.1");

        // Keep trying all the way through the window: the client still gets in when it ends.
        for (var i = 0; i < 9; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);
        }

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
    }

    [Test]
    public void ANewWindowStartsFresh()
    {
        for (var i = 0; i < 3; i++)
            _limiter.TryAcquire("1.1.1.1");

        _time.Advance(Window);

        for (var i = 0; i < 3; i++)
            Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.True);
        Assert.That(_limiter.TryAcquire("1.1.1.1"), Is.False);
    }

    [Test]
    public void ForgetsClientsWhoseWindowIsOver()
    {
        for (var i = 0; i < 100; i++)
            _limiter.TryAcquire($"client-{i}");

        _time.Advance(Window);
        _limiter.TryAcquire("someone-else");

        Assert.That(_limiter.TrackedClients, Is.EqualTo(1));
    }

    [Test]
    public void DefaultsLimitAttemptsPerWindow()
    {
        var limiter = new SignUpRateLimiter();

        for (var i = 0; i < SignUpRateLimiter.DEFAULT_ATTEMPTS; i++)
            Assert.That(limiter.TryAcquire("1.1.1.1"), Is.True);

        Assert.That(limiter.TryAcquire("1.1.1.1"), Is.False);
        Assert.That(limiter.Window, Is.EqualTo(SignUpRateLimiter.DefaultWindow));
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
