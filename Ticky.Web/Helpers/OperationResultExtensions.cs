using Ticky.Web.Components.Layout;

namespace Ticky.Web.Helpers;

/// <summary>Shows the outcome of a service call in the web UI.</summary>
public static class OperationResultExtensions
{
    /// <summary>Tells the user why the change failed, if it did. Returns whether it succeeded.</summary>
    public static bool Report(this MainLayout layout, IOperationResult result)
    {
        if (result.Succeeded)
            return true;

        layout.RunNotification(new Notification(result.Message ?? "Something went wrong.", NotificationType.Fail));
        return false;
    }

    /// <summary>
    /// Like <see cref="Report"/>, for a change made in a dialog: if it was refused for access (the role may have changed
    /// since the dialog opened) or what it changed is gone, nothing in the dialog can fix that, so
    /// <paramref name="close"/> closes it. An invalid value leaves it open to correct.
    /// </summary>
    public static async Task<bool> ReportOrCloseAsync(this MainLayout layout, IOperationResult result, Func<Task> close)
    {
        if (layout.Report(result))
            return true;

        if (result.Status is OperationStatus.Forbidden or OperationStatus.NotFound)
            await close();

        return false;
    }

    /// <summary>
    /// Like <see cref="Report"/>, for a change made on a page: if what it changed is gone, e.g. deleted in another tab,
    /// <paramref name="reload"/> shows the page as it is now.
    /// </summary>
    public static async Task<bool> ReportOrReloadAsync(this MainLayout layout, IOperationResult result, Func<Task> reload)
    {
        if (layout.Report(result))
            return true;

        if (result.Status == OperationStatus.NotFound)
            await reload();

        return false;
    }

    /// <summary>
    /// Saves a change the page already shows, like a drag and drop (see <see cref="TrySaveAsync"/>). If it fails, tells
    /// the user why and calls <paramref name="revert"/> to put things back. Returns whether it was saved.
    /// </summary>
    public static async Task<bool> SaveOrRevertAsync<TResult>(this MainLayout layout, ILogger logger, Func<Task<TResult>> change, string failure, Func<Task> revert)
        where TResult : IOperationResult
    {
        if (layout.Report(await logger.TrySaveAsync(change, failure)))
            return true;

        await revert();
        return false;
    }

    /// <inheritdoc cref="SaveOrRevertAsync{TResult}(MainLayout, ILogger, Func{Task{TResult}}, string, Func{Task})"/>
    public static Task<bool> SaveOrRevertAsync<TResult>(this MainLayout layout, ILogger logger, Func<Task<TResult>> change, string failure, Action revert)
        where TResult : IOperationResult =>
        layout.SaveOrRevertAsync(logger, change, failure, () =>
        {
            revert();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Makes a change through a service. An unexpected error is logged and comes back as a failure with
    /// <paramref name="failure"/> as its message, so the page handles it like a refused change; after a drag and drop,
    /// that puts things back.
    /// </summary>
    public static async Task<IOperationResult> TrySaveAsync<TResult>(this ILogger logger, Func<Task<TResult>> change, string failure)
        where TResult : IOperationResult
    {
        try
        {
            return await change();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Failure}", failure);
            return OperationResult.Conflict(failure);
        }
    }
}
