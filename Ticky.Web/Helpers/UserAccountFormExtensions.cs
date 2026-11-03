using Microsoft.AspNetCore.Components.Forms;

namespace Ticky.Web.Helpers;

/// <summary>Shows errors from <see cref="UserAccountService"/> on the account forms.</summary>
public static class UserAccountFormExtensions
{
    /// <summary>
    /// Creates a store for errors the server found. An error goes away once its field is edited or the form is
    /// submitted again.
    /// </summary>
    public static ValidationMessageStore CreateServerErrorStore(this EditContext editContext)
    {
        var store = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += (_, _) => store.Clear();
        editContext.OnFieldChanged += (_, e) => store.Clear(e.FieldIdentifier);
        return store;
    }

    /// <summary>
    /// Shows each error next to its field, given as pairs of a field and the model property bound to it. Returns the
    /// errors that belong to none of those fields, joined, or null when there are none.
    /// </summary>
    public static string? ShowOn(
        this UserAccountResult result,
        EditContext editContext,
        ValidationMessageStore store,
        params (UserAccountField Field, string Property)[] fields
    )
    {
        var unplaced = new List<string>();

        foreach (var error in result.Errors)
        {
            var property = fields.FirstOrDefault(x => x.Field == error.Field).Property;

            if (property is null)
                unplaced.Add(error.Message);
            else
                store.Add(editContext.Field(property), error.Message);
        }

        editContext.NotifyValidationStateChanged();
        return unplaced.Count == 0 ? null : string.Join(" ", unplaced);
    }
}
