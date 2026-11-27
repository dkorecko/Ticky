using System.Drawing;

namespace Ticky.Internal.Services;

/// <summary>
/// A board's labels, for both the web UI and the API. Only board admins may create, change or delete them (see
/// <see cref="Board.IsAdmin(int)"/>); every member can put them on cards (see <see cref="CardService.SetLabelAsync"/>).
/// </summary>
public class LabelService(IDbContextFactory<DataContext> dbContextFactory)
{
    private const string LABEL_NOT_FOUND = "The label doesn't exist, or you can't access it.";
    private const string MANAGE_LABELS = "manage labels";

    /// <summary>Changes to a label; null leaves a value as it is.</summary>
    public record LabelChanges
    {
        public string? Name { get; init; }
        public Color? TextColor { get; init; }
        public Color? BackgroundColor { get; init; }
    }

    public async Task<OperationResult<Label>> CreateAsync(int boardId, int userId, string name, Color textColor, Color backgroundColor)
    {
        name = name?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(name, nameof(Label.Name)) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, MANAGE_LABELS) is { } denied)
            return denied;

        var label = new Label
        {
            BoardId = boardId,
            Name = name,
            TextColor = textColor,
            BackgroundColor = backgroundColor,
        };

        db.Labels.Add(label);
        await db.SaveChangesAsync();
        return label;
    }

    public async Task<OperationResult<Label>> UpdateAsync(int labelId, int userId, LabelChanges changes)
    {
        var name = changes.Name?.Trim();

        if (name is not null && OperationResult.RequireText(name, nameof(Label.Name)) is { } invalid)
            return invalid;

        using var db = dbContextFactory.CreateDbContext();
        var label = await db.Labels.FirstOrDefaultAsync(x => x.Id == labelId);

        if (await db.CheckBoardAdminAsync(label?.BoardId, userId, MANAGE_LABELS, LABEL_NOT_FOUND) is { } denied)
            return denied;

        label!.Name = name ?? label.Name;
        label.TextColor = changes.TextColor ?? label.TextColor;
        label.BackgroundColor = changes.BackgroundColor ?? label.BackgroundColor;

        await db.SaveChangesAsync();
        return label;
    }

    /// <summary>Deletes the label, which also takes it off every card.</summary>
    public async Task<OperationResult> DeleteAsync(int labelId, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();
        var label = await db.Labels.FirstOrDefaultAsync(x => x.Id == labelId);

        if (await db.CheckBoardAdminAsync(label?.BoardId, userId, MANAGE_LABELS, LABEL_NOT_FOUND) is { } denied)
            return denied;

        db.Labels.Remove(label!);
        await db.SaveChangesAsync();
        return OperationResult.Success;
    }
}
