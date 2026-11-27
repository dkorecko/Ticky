using System.Drawing;
using Microsoft.EntityFrameworkCore;
using Ticky.Base.DTOs;

namespace Ticky.Units;

/// <summary>A board's columns and labels, which only its admins manage.</summary>
public class ColumnAndLabelServiceTest
{
    private TestDatabase _db = null!;
    private ColumnService _columns = null!;
    private LabelService _labels = null!;

    [SetUp]
    public async Task Setup()
    {
        _db = await TestDatabase.CreateAsync();
        _columns = new ColumnService(_db.Factory);
        _labels = new LabelService(_db.Factory);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<string[]> ColumnOrderAsync()
    {
        using var db = _db.CreateDbContext();
        return await db.Columns.OrderBy(x => x.Index).Select(x => x.Name).ToArrayAsync();
    }

    [Test]
    public async Task CreateColumn_AdminAddsLast_MemberForbidden()
    {
        Assert.That((await _columns.CreateAsync(_db.BoardId, _db.BoardMemberId, new("Nope"))).Status, Is.EqualTo(OperationStatus.Forbidden));

        var result = await _columns.CreateAsync(_db.BoardId, _db.BoardAdminId, new(" Done ") { Finished = true, MaxCards = 5 });

        Assert.That(result.Value!.Finished, Is.True);
        Assert.That(await ColumnOrderAsync(), Is.EqualTo(new[] { "To do", "Done" }));
    }

    [Test]
    public async Task CreateColumn_MarkedMigrated_SoStartupKeepsItsPlacement()
    {
        var created = await _columns.CreateAsync(_db.BoardId, _db.BoardAdminId, new("Inbox") { NewCardPlacement = CardPlacement.Top });

        // On every startup, DataMigrator resets the placement of columns that aren't marked migrated yet.
        using var db = _db.CreateDbContext();
#pragma warning disable CS0618
        var column = await db.Columns.SingleAsync(x => x.Id == created.Value!.Id);
        Assert.That(column.OrderRule, Is.EqualTo(OrderRule.Migrated));
#pragma warning restore CS0618
    }

    [Test]
    public async Task UpdateColumn_ChangesOnlyWhatIsSent()
    {
        var result = await _columns.UpdateAsync(_db.ColumnId, _db.ProjectAdminId, new() { Collapsed = true, MaxCards = 3 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Name, Is.EqualTo("To do"));
            Assert.That(result.Value.Collapsed, Is.True);
            Assert.That(result.Value.MaxCards, Is.EqualTo(3));
        });
    }

    [TestCase("", null)]
    [TestCase(null, -1)]
    public async Task UpdateColumn_BadValues_Invalid(string? name, int? maxCards) =>
        Assert.That((await _columns.UpdateAsync(_db.ColumnId, _db.BoardAdminId, new() { Name = name, MaxCards = maxCards })).Status, Is.EqualTo(OperationStatus.Invalid));

    [Test]
    public async Task UpdateColumn_Outsider_NotFound() =>
        Assert.That((await _columns.UpdateAsync(_db.ColumnId, _db.OutsiderId, new() { Name = "X" })).Status, Is.EqualTo(OperationStatus.NotFound));

    [Test]
    public async Task MoveAndDeleteColumns_KeepIndicesInOrder()
    {
        await _db.AddColumnAsync("Doing");
        var doneId = await _db.AddColumnAsync("Done");

        await _columns.MoveAsync(doneId, _db.BoardAdminId, _db.ColumnId);
        Assert.That(await ColumnOrderAsync(), Is.EqualTo(new[] { "Done", "To do", "Doing" }));

        await _columns.DeleteAsync(_db.ColumnId, _db.BoardAdminId);
        Assert.That(await ColumnOrderAsync(), Is.EqualTo(new[] { "Done", "Doing" }));

        using var db = _db.CreateDbContext();
        Assert.That(await db.Columns.OrderBy(x => x.Index).Select(x => x.Index).ToArrayAsync(), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(await db.Cards.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Labels_OnlyAdminsManageThem()
    {
        Assert.That((await _labels.CreateAsync(_db.BoardId, _db.BoardMemberId, "Bug", Color.White, Color.Red)).Status, Is.EqualTo(OperationStatus.Forbidden));

        var label = (await _labels.CreateAsync(_db.BoardId, _db.BoardAdminId, "Bug", Color.White, Color.Red)).Value!;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _labels.UpdateAsync(label.Id, _db.BoardMemberId, new() { Name = "Defect" })).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _labels.UpdateAsync(label.Id, _db.OutsiderId, new() { Name = "Defect" })).Status, Is.EqualTo(OperationStatus.NotFound));
            Assert.That((await _labels.UpdateAsync(label.Id, _db.BoardAdminId, new() { BackgroundColor = Color.Blue })).Value!.Name, Is.EqualTo("Bug"));
            Assert.That((await _labels.DeleteAsync(label.Id, _db.BoardMemberId)).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _labels.DeleteAsync(label.Id, _db.BoardAdminId)).Succeeded, Is.True);
            Assert.That((await _labels.DeleteAsync(label.Id, _db.BoardAdminId)).Status, Is.EqualTo(OperationStatus.NotFound));
        });
    }
}
