using Microsoft.Extensions.Hosting;

namespace Ticky.Internal.Services.Hosted;

public abstract class AbstractHostedService<T> : IHostedService
{
    protected ILogger<T> Logger { get; set; }
    protected readonly IServiceScopeFactory ServiceScopeFactory;
    private Timer _timer;
    private bool _unrealizedChange;
    private TimeSpan _untilStart;
    private TimeSpan _frequency;

    protected AbstractHostedService(
        IServiceScopeFactory serviceScopeFactory,
        TimeSpan untilStart,
        TimeSpan frequency
    )
    {
        if (untilStart < TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY))
            throw new Exception(
                $"A hosted service cannot have a minimum time shorter than {Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY} seconds."
            );
        ServiceScopeFactory = serviceScopeFactory;
        _untilStart = untilStart;
        _frequency = frequency;
        _timer = new(DoWork, null, untilStart, frequency);
        Logger = ServiceScopeFactory
            .CreateScope()
            .ServiceProvider.GetRequiredService<ILogger<T>>()!;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        Logger.LogDebug("{Name} starting.", GetType().Name);

        OnStart();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        Logger.LogDebug("{Name} stopping.", GetType().Name);

        _timer!.Change(Timeout.Infinite, 0);
        Dispose();
        OnStop();
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            var startedAt = DateTime.Now;
            using var scope = ServiceScopeFactory.CreateScope();

            Logger.LogDebug("{Name} job running.", GetType().Name);

            await OnRun();

            Logger.LogDebug(
                "{Name} job finished, next run will happen at {GetNextRunDateTime}.", GetType().Name, GetNextRunDateTime(startedAt)
            );
        } 
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }
    }

    private string GetNextRunDateTime(DateTime startedAt)
    {
        if (_unrealizedChange)
        {
            _unrealizedChange = false;
            return startedAt.AddSeconds(_untilStart.TotalSeconds).ToReadableStringWithTime();
        }
        else
            return startedAt.AddSeconds(_frequency.TotalSeconds).ToReadableStringWithTime();
    }

    protected void ChangeTimer(TimeSpan untilStart, TimeSpan frequency)
    {
        _unrealizedChange = true;
        _untilStart = untilStart;
        _frequency = frequency;
        _timer.Change(_untilStart, _frequency);
    }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

    protected virtual Task OnRun()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer!.Dispose();
    }
}
