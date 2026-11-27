using Microsoft.EntityFrameworkCore;
using Ticky.Base.DTOs;

namespace Ticky.Units;

/// <summary>Runs CardService against an in-memory SQLite database.</summary>
public class CardServiceTest
{
    private TestDatabase _db = null!;
    private CardService _service = null!;

    private int _boardMemberId;
    private int _projectMemberId;
    private int _outsiderId;
    private int _cardId;
    private int _otherCardId;
    private int _thirdCardId;

    [SetUp]
    public async Task Setup()
    {
        _db = await TestDatabase.CreateAsync();
        _service = new CardService(_db.Factory, new CardNumberingService(_db.Factory));

        (_boardMemberId, _projectMemberId, _outsiderId) = (_db.BoardMemberId, _db.ProjectMemberId, _db.OutsiderId);
        (_cardId, _otherCardId, _thirdCardId) = (_db.CardIds[0], _db.CardIds[1], _db.CardIds[2]);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<int> ActivityCountAsync(int cardId)
    {
        using var db = _db.CreateDbContext();
        return await db.Activities.CountAsync(x => x.CardId == cardId);
    }

    #region Access

    [Test]
    public async Task CanAccessCard_BoardMember_True() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _boardMemberId), Is.True);

    [Test]
    public async Task CanAccessCard_ProjectMember_True() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _projectMemberId), Is.True);

    [Test]
    public async Task CanAccessCard_Outsider_False() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _outsiderId), Is.False);

    [Test]
    public async Task CanAccessCard_MissingCard_False() =>
        Assert.That(await _service.CanAccessCardAsync(999_999, _boardMemberId), Is.False);

    /// <summary>Every change, made by someone who can't open the card's board.</summary>
    private static readonly (string Name, Func<CardService, int, int, Task<IOperationResult>> Change)[] OutsiderChanges =
    [
        ("Rename", async (s, card, user) => await s.RenameAsync(card, user, "Taken")),
        ("UpdateDescription", async (s, card, user) => await s.UpdateDescriptionAsync(card, user, "Taken")),
        ("SetPriority", async (s, card, user) => await s.SetPriorityAsync(card, user, CardPriority.Critical)),
        ("SetFlagged", async (s, card, user) => await s.SetFlaggedAsync(card, user, true)),
        ("SetDeadline", async (s, card, user) => await s.SetDeadlineAsync(card, user, DateTime.Today)),
        ("Snooze", async (s, card, user) => await s.SnoozeAsync(card, user, DateTime.Today.AddDays(1))),
        ("Delete", async (s, card, user) => await s.DeleteAsync(card, user)),
        ("Move", async (s, card, user) => await s.MoveAsync(card, user, 0, null)),
        ("MoveToAdjacentColumn", async (s, card, user) => await s.MoveToAdjacentColumnAsync(card, user, 1)),
        ("SetRepeat", async (s, card, user) => await s.SetRepeatAsync(card, user, new RepeatInfo { Type = RepeatType.Daily, Time = new TimeOnly(9, 0), CardPlacement = CardPlacement.Top, TargetColumnId = 1 })),
        ("SetLabel", async (s, card, user) => await s.SetLabelAsync(card, user, 1, true)),
        ("SetAssignee", async (s, card, user) => await s.SetAssigneeAsync(card, user, user, true)),
        ("AddComment", async (s, card, user) => await s.AddCommentAsync(card, user, "Taken")),
        ("AddSubtask", async (s, card, user) => await s.AddSubtaskAsync(card, user, "Taken")),
        ("AddReminder", async (s, card, user) => await s.AddReminderAsync(card, user, DateTime.Today)),
        ("AddAttachment", async (s, card, user) => await s.AddAttachmentAsync(card, user, "x.bin", "x.txt")),
        ("StartTimer", async (s, card, user) => await s.StartTimerAsync(card, user)),
        ("AddTimeRecord", async (s, card, user) => await s.AddTimeRecordAsync(card, user, TimeSpan.FromHours(1))),
        ("LinkCards", async (s, card, user) => await s.LinkCardsAsync(card, user, card + 1, "relates to")),
    ];

    private static IEnumerable<TestCaseData> OutsiderChangeCases() =>
        OutsiderChanges.Select(x => new TestCaseData(x.Change).SetName($"Outsider_{x.Name}_NotFoundAndNothingChanges"));

    [TestCaseSource(nameof(OutsiderChangeCases))]
    public async Task Outsider_Change_NotFoundAndNothingChanges(Func<CardService, int, int, Task<IOperationResult>> change)
    {
        var result = await change(_service, _cardId, _outsiderId);

        using var db = _db.CreateDbContext();
        var card = await db.Cards.SingleAsync(x => x.Id == _cardId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Status, Is.EqualTo(OperationStatus.NotFound));
            Assert.That(card.Name, Is.EqualTo("Card 1"));
            Assert.That(card.Flagged, Is.False);
            Assert.That(await db.Activities.CountAsync(), Is.Zero);
            Assert.That(await db.Comments.CountAsync() + await db.Subtasks.CountAsync() + await db.TimeRecords.CountAsync(), Is.Zero);
        });
    }

    [TestCase("board member", true)]
    [TestCase("project member", true)]
    [TestCase("outsider", false)]
    public async Task AccessibleBy_AppliesTheBoardRuleToBoardsColumnsAndCards(string who, bool canOpen)
    {
        var userId = who switch
        {
            "board member" => _boardMemberId,
            "project member" => _projectMemberId,
            _ => _outsiderId,
        };

        using var db = _db.CreateDbContext();

        Assert.That(await db.Boards.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 1 : 0));
        Assert.That(await db.Columns.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 1 : 0));
        Assert.That(await db.Cards.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 3 : 0));
    }

    #endregion

    #region Card fields

    [Test]
    public async Task Activity_StoresRawValueAndEscapesWhenShown()
    {
        await _service.AddSubtaskAsync(_cardId, _boardMemberId, "<img src=x onerror=alert(1)>");

        using var db = _db.CreateDbContext();
        var activity = (SubtaskAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.SubtaskAdded));
            Assert.That(activity.LegacyText, Is.Null);
            Assert.That(activity.SubtaskText, Is.EqualTo("<img src=x onerror=alert(1)>"));
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>added</b> subtask <b>&lt;img src=x onerror=alert(1)&gt;</b>"));
            Assert.That(activity.ToText(), Is.EqualTo("added subtask <img src=x onerror=alert(1)>"));
        });
    }

    [Test]
    public async Task Rename_KeepsOldAndNewTitle()
    {
        await _service.RenameAsync(_cardId, _boardMemberId, "Renamed");

        using var db = _db.CreateDbContext();
        var activity = (TitleChangedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.ToHtml(), Is.EqualTo("<b>changed</b> the title from <b>Card 1</b> to <b>Renamed</b>"));
    }

    [Test]
    public async Task Rename_SameName_LogsNothing()
    {
        await _service.RenameAsync(_cardId, _boardMemberId, "Card 1");

        Assert.That(await ActivityCountAsync(_cardId), Is.Zero);
    }

    [Test]
    public async Task Rename_Blank_InvalidAndUnchanged()
    {
        var result = await _service.RenameAsync(_cardId, _boardMemberId, "   ");

        using var db = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).Name, Is.EqualTo("Card 1"));
    }

    [Test]
    public async Task SetDeadline_SameDeadline_KeepsReminderStateAndLogsOnce()
    {
        var deadline = new DateTime(2030, 1, 2, 9, 0, 0);
        await _service.SetDeadlineAsync(_cardId, _boardMemberId, deadline);

        using (var db = _db.CreateDbContext())
        {
            (await db.Cards.SingleAsync(x => x.Id == _cardId)).DeadlineProcessed = true;
            await db.SaveChangesAsync();
        }

        await _service.SetDeadlineAsync(_cardId, _boardMemberId, deadline);

        using var check = _db.CreateDbContext();
        Assert.That((await check.Cards.SingleAsync(x => x.Id == _cardId)).DeadlineProcessed, Is.True);
        Assert.That(await check.Activities.CountAsync(x => x.CardId == _cardId), Is.EqualTo(1));
    }

    [Test]
    public async Task RemoveDeadline_NoDeadline_LogsNothing()
    {
        await _service.RemoveDeadlineAsync(_cardId, _boardMemberId);

        Assert.That(await ActivityCountAsync(_cardId), Is.Zero);
    }

    [Test]
    public async Task SetFlagged_SetsAndClearsWithoutLogging()
    {
        await _service.SetFlaggedAsync(_cardId, _boardMemberId, true);

        using (var db = _db.CreateDbContext())
            Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).Flagged, Is.True);

        await _service.SetFlaggedAsync(_cardId, _boardMemberId, false);

        using var check = _db.CreateDbContext();
        Assert.That((await check.Cards.SingleAsync(x => x.Id == _cardId)).Flagged, Is.False);
        Assert.That(await check.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    [Test]
    public async Task Snooze_SameTimeTwice_LogsOnce()
    {
        var until = new DateTime(2030, 1, 1, 8, 0, 0);
        await _service.SnoozeAsync(_cardId, _boardMemberId, until);
        await _service.SnoozeAsync(_cardId, _boardMemberId, until);
        await _service.UnsnoozeAsync(_cardId, _boardMemberId);
        await _service.UnsnoozeAsync(_cardId, _boardMemberId);

        Assert.That(await ActivityCountAsync(_cardId), Is.EqualTo(2));
    }

    #endregion

    #region Repeat

    private static RepeatInfo Repeat(RepeatType type, string? selected = null, int? number = null, int? targetColumnId = null) =>
        new()
        {
            Type = type,
            Time = new TimeOnly(9, 0),
            Selected = selected,
            Number = number,
            CardPlacement = CardPlacement.Top,
            TargetColumnId = targetColumnId,
        };

    [Test]
    public async Task SetRepeat_ColumnOnAnotherBoard_InvalidAndNotSet()
    {
        // Older versions stored any column id, so the repeat job created cards on boards the user couldn't open.
        var (_, otherColumnId, _) = await _db.AddOtherBoardAsync(_outsiderId);

        var result = await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(RepeatType.Daily, targetColumnId: otherColumnId));

        using var db = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo(nameof(RepeatInfo.TargetColumnId)));
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo, Is.Null);
    }

    [TestCase(RepeatType.WeekDays, "Mon,Funday", null, nameof(RepeatInfo.Selected))]
    [TestCase(RepeatType.WeekDays, null, null, nameof(RepeatInfo.Selected))]
    [TestCase(RepeatType.WeekDays, "Sun\n", null, nameof(RepeatInfo.Selected))]
    [TestCase(RepeatType.MonthDayNumber, "15\n", null, nameof(RepeatInfo.Selected))]
    [TestCase(RepeatType.MonthDayNumber, "1,32", null, nameof(RepeatInfo.Selected))]
    [TestCase(RepeatType.EveryXthDay, null, null, nameof(RepeatInfo.Number))]
    [TestCase(RepeatType.EveryXthWeek, null, 1000, nameof(RepeatInfo.Number))]
    public async Task SetRepeat_IncompleteSchedule_Invalid(RepeatType type, string? selected, int? number, string field)
    {
        var result = await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(type, selected, number, _db.ColumnId));

        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo(field));
    }

    [Test]
    public async Task SetRepeat_StartTooFarAhead_Invalid()
    {
        var repeat = Repeat(RepeatType.EveryXthYear, number: 999, targetColumnId: _db.ColumnId);
        repeat.LastRepeat = new DateTime(9999, 12, 31);

        var result = await _service.SetRepeatAsync(_cardId, _boardMemberId, repeat);

        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo(nameof(RepeatInfo.LastRepeat)));
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(999_999)]
    public async Task SetRepeat_NoUsableTargetColumn_InvalidAndNothingChanges(int? targetColumnId)
    {
        var result = await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(RepeatType.Daily, targetColumnId: targetColumnId));

        using var db = _db.CreateDbContext();
        Assert.That(result.Field, Is.EqualTo(nameof(RepeatInfo.TargetColumnId)));
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo, Is.Null);
        Assert.That(await db.Activities.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task SetRepeat_ForeignColumnOnCardWithRepeat_KeepsTheEarlierRepeat()
    {
        var (_, otherColumnId, _) = await _db.AddOtherBoardAsync(_outsiderId);
        await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(RepeatType.Daily, targetColumnId: _db.ColumnId));

        Assert.That((await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(RepeatType.Daily, targetColumnId: otherColumnId))).Succeeded, Is.False);

        using var db = _db.CreateDbContext();
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo!.TargetColumnId, Is.EqualTo(_db.ColumnId));
    }

    [Test]
    public async Task SetRepeat_Valid_ClearsUnusedValuesAndLogs()
    {
        var result = await _service.SetRepeatAsync(_cardId, _boardMemberId, Repeat(RepeatType.WeekDays, "Mon,Thu", number: 5, targetColumnId: _db.ColumnId));

        using var db = _db.CreateDbContext();
        var repeat = (await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo!;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(repeat.Selected, Is.EqualTo("Mon,Thu"));
            Assert.That(repeat.Number, Is.Null);
            Assert.That(repeat.TargetColumnId, Is.EqualTo(_db.ColumnId));
            Assert.That(await ActivityCountAsync(_cardId), Is.EqualTo(1));
        });
    }

    #endregion

    #region Moving and deleting

    [Test]
    public async Task Move_RecordsTypedMoveWithColumnSnapshot()
    {
        var doneId = await _db.AddColumnAsync("Done & dusted", finished: true);

        Assert.That((await _service.MoveAsync(_cardId, _boardMemberId, doneId, null)).Succeeded, Is.True);

        using var db = _db.CreateDbContext();
        var activity = (CardMovedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.CardMoved));
            Assert.That(activity.FromColumnName, Is.EqualTo("To do"));
            Assert.That(activity.ToColumnId, Is.EqualTo(doneId));
            Assert.That(activity.ToColumnName, Is.EqualTo("Done & dusted"));
            Assert.That(activity.ToColumnFinished, Is.True);
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>moved</b> the card from <b>To do</b> to <b>Done &amp; dusted</b>"));
        });
    }

    [Test]
    public async Task Move_PlacesBeforeTargetCardAndFixesIndices()
    {
        var doneId = await _db.AddColumnAsync("Done", finished: true);

        await _service.MoveAsync(_cardId, _boardMemberId, doneId, null);
        await _service.MoveAsync(_otherCardId, _boardMemberId, doneId, _cardId);

        using var db = _db.CreateDbContext();
        var cards = await db.Cards.ToDictionaryAsync(x => x.Id);

        Assert.Multiple(() =>
        {
            Assert.That(cards[_otherCardId].ColumnId, Is.EqualTo(doneId));
            Assert.That(cards[_otherCardId].Index, Is.EqualTo(0));
            Assert.That(cards[_cardId].Index, Is.EqualTo(1));
            Assert.That(cards[_thirdCardId].Index, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Move_FullColumn_ConflictAndChangesNothing()
    {
        var doneId = await _db.AddColumnAsync("Done", finished: true, maxCards: 1);
        await _service.MoveAsync(_cardId, _boardMemberId, doneId, null);

        Assert.That((await _service.MoveAsync(_otherCardId, _boardMemberId, doneId, null)).Status, Is.EqualTo(OperationStatus.Conflict));

        using var db = _db.CreateDbContext();
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _otherCardId)).ColumnId, Is.Not.EqualTo(doneId));
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _otherCardId), Is.Zero);
    }

    [Test]
    public async Task Move_WithinItsColumn_ReordersWithoutLogging()
    {
        await _service.MoveAsync(_thirdCardId, _boardMemberId, _db.ColumnId, _cardId);

        using var db = _db.CreateDbContext();
        var order = await db.Cards.OrderBy(x => x.Index).Select(x => x.Id).ToListAsync();

        Assert.That(order, Is.EqualTo(new[] { _thirdCardId, _cardId, _otherCardId }));
        Assert.That(await db.Activities.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Move_ToColumnOnAnotherBoard_Invalid()
    {
        var (_, otherColumnId, _) = await _db.AddOtherBoardAsync(_boardMemberId);

        var result = await _service.MoveAsync(_cardId, _boardMemberId, otherColumnId, null);

        using var db = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).ColumnId, Is.EqualTo(_db.ColumnId));
    }

    [Test]
    public async Task MoveToAdjacentColumn_GoesToTheEndOfTheNextColumn()
    {
        var doneId = await _db.AddColumnAsync("Done", finished: true);
        await _service.MoveAsync(_otherCardId, _boardMemberId, doneId, null);

        await _service.MoveToAdjacentColumnAsync(_cardId, _boardMemberId, 1);

        using var db = _db.CreateDbContext();
        var done = await db.Cards.Where(x => x.ColumnId == doneId).OrderBy(x => x.Index).Select(x => x.Id).ToListAsync();
        Assert.That(done, Is.EqualTo(new[] { _otherCardId, _cardId }));
    }

    [Test]
    public async Task Delete_RemovesCardAndClosesTheGap()
    {
        Assert.That((await _service.DeleteAsync(_cardId, _boardMemberId)).Succeeded, Is.True);

        using var db = _db.CreateDbContext();
        var cards = await db.Cards.OrderBy(x => x.Index).Select(x => new { x.Id, x.Index }).ToListAsync();

        Assert.That(cards.Select(x => x.Id), Is.EqualTo(new[] { _otherCardId, _thirdCardId }));
        Assert.That(cards.Select(x => x.Index), Is.EqualTo(new[] { 0, 1 }));
    }

    #endregion

    #region Creating

    [Test]
    public async Task CreateCard_ProjectMember_AddsTrimmedCardAtBottom()
    {
        var columnId = await _db.AddColumnAsync("Doing");
        await _service.CreateCardAsync(columnId, _projectMemberId, new("First"));
        var result = await _service.CreateCardAsync(columnId, _projectMemberId, new("  Second  "));

        Assert.That(result.Succeeded, Is.True);

        using var db = _db.CreateDbContext();
        var cards = await db.Cards.Where(x => x.ColumnId == columnId).OrderBy(x => x.Index).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cards.Select(x => x.Name), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(cards[1].Number, Is.GreaterThan(cards[0].Number));
            Assert.That(cards[1].CreatedById, Is.EqualTo(_projectMemberId));
        });
    }

    [Test]
    public async Task CreateCard_TopPlacement_PutsNewCardFirst()
    {
        var columnId = await _db.AddColumnAsync("Doing");
        await _service.CreateCardAsync(columnId, _boardMemberId, new("Older"));

        using (var db = _db.CreateDbContext())
        {
            (await db.Columns.SingleAsync(x => x.Id == columnId)).NewCardPlacement = CardPlacement.Top;
            await db.SaveChangesAsync();
        }

        await _service.CreateCardAsync(columnId, _boardMemberId, new("Newer"));

        using var check = _db.CreateDbContext();
        var names = await check.Cards.Where(x => x.ColumnId == columnId).OrderBy(x => x.Index).Select(x => x.Name).ToListAsync();
        Assert.That(names, Is.EqualTo(new[] { "Newer", "Older" }));
    }

    [Test]
    public async Task CreateCard_Outsider_NotFoundAndNothingCreated()
    {
        var columnId = await _db.AddColumnAsync("Doing");
        var result = await _service.CreateCardAsync(columnId, _outsiderId, new("Sneaky"));

        using var db = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.NotFound));
        Assert.That(await db.Cards.CountAsync(x => x.ColumnId == columnId), Is.Zero);
    }

    [Test]
    public async Task CreateCard_FullColumn_ConflictAndNothingCreated()
    {
        var columnId = await _db.AddColumnAsync("Done", finished: true, maxCards: 1);

        Assert.That((await _service.CreateCardAsync(columnId, _boardMemberId, new("Fits"))).Succeeded, Is.True);
        Assert.That((await _service.CreateCardAsync(columnId, _boardMemberId, new("Too many"))).Status, Is.EqualTo(OperationStatus.Conflict));

        using var db = _db.CreateDbContext();
        Assert.That(await db.Cards.CountAsync(x => x.ColumnId == columnId), Is.EqualTo(1));
    }

    [Test]
    public async Task CreateCard_BlankName_Invalid()
    {
        var result = await _service.CreateCardAsync(_db.ColumnId, _boardMemberId, new("   "));

        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo(nameof(CardService.CardDraft.Name)));
    }

    [Test]
    public async Task CreateCard_WithEverything_SetsItAllWithoutLogging()
    {
        int labelId;

        using (var db = _db.CreateDbContext())
        {
            var label = new Label { Name = "Bug", BoardId = _db.BoardId, TextColor = System.Drawing.Color.White, BackgroundColor = System.Drawing.Color.Red };
            db.Labels.Add(label);
            await db.SaveChangesAsync();
            labelId = label.Id;
        }

        var deadline = new DateTime(2030, 5, 1, 12, 0, 0);
        var result = await _service.CreateCardAsync(_db.ColumnId, _boardMemberId, new("Full")
        {
            Description = "Details",
            Priority = CardPriority.High,
            Deadline = deadline,
            LabelIds = [labelId],
            AssigneeIds = [_boardMemberId, _projectMemberId],
        });

        using var check = _db.CreateDbContext();
        var card = await check.Cards.Include(x => x.Labels).Include(x => x.Assignees).SingleAsync(x => x.Id == result.Value!.Id);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(card.Description, Is.EqualTo("Details"));
            Assert.That(card.Priority, Is.EqualTo(CardPriority.High));
            Assert.That(card.Deadline, Is.EqualTo(deadline));
            Assert.That(card.Labels.Select(x => x.Id), Is.EqualTo(new[] { labelId }));
            Assert.That(card.Assignees.Select(x => x.Id), Is.EquivalentTo(new[] { _boardMemberId, _projectMemberId }));
            Assert.That(await check.Activities.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task CreateCard_OutsiderAssigneeOrForeignLabel_InvalidAndNothingCreated()
    {
        var byAssignee = await _service.CreateCardAsync(_db.ColumnId, _boardMemberId, new("A") { AssigneeIds = [_outsiderId] });
        var byLabel = await _service.CreateCardAsync(_db.ColumnId, _boardMemberId, new("B") { LabelIds = [999] });

        using var db = _db.CreateDbContext();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(byAssignee.Field, Is.EqualTo(nameof(CardService.CardDraft.AssigneeIds)));
            Assert.That(byLabel.Field, Is.EqualTo(nameof(CardService.CardDraft.LabelIds)));
            Assert.That(await db.Cards.CountAsync(), Is.EqualTo(3));
        });
    }

    #endregion

    #region Assignees, labels and comments

    [Test]
    public async Task SetAssignee_Self_IsShownAsJoiningAndOnlyLoggedOnce()
    {
        await _service.SetAssigneeAsync(_cardId, _boardMemberId, _boardMemberId, true);
        var again = await _service.SetAssigneeAsync(_cardId, _boardMemberId, _boardMemberId, true);

        using var db = _db.CreateDbContext();
        var activity = (AssigneeAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(again.Value!.Select(x => x.Id), Is.EqualTo(new[] { _boardMemberId }));
            Assert.That(activity.AssigneeName, Is.EqualTo("Board member"));
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>joined</b> the card"));
        });
    }

    [Test]
    public async Task SetAssignee_Outsider_InvalidButCanStillBeUnassigned()
    {
        Assert.That((await _service.SetAssigneeAsync(_cardId, _boardMemberId, _outsiderId, true)).Status, Is.EqualTo(OperationStatus.Invalid));

        // Someone who left the board stays on its cards until taken off.
        using (var db = _db.CreateDbContext())
        {
            var card = await db.Cards.Include(x => x.Assignees).SingleAsync(x => x.Id == _cardId);
            card.Assignees.Add(await db.Users.SingleAsync(x => x.Id == _outsiderId));
            await db.SaveChangesAsync();
        }

        var result = await _service.SetAssigneeAsync(_cardId, _boardMemberId, _outsiderId, false);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task SetLabel_LabelOfAnotherBoard_Invalid()
    {
        var (otherBoardId, _, _) = await _db.AddOtherBoardAsync(_boardMemberId);
        int labelId;

        using (var db = _db.CreateDbContext())
        {
            var label = new Label { Name = "Elsewhere", BoardId = otherBoardId, TextColor = System.Drawing.Color.White, BackgroundColor = System.Drawing.Color.Red };
            db.Labels.Add(label);
            await db.SaveChangesAsync();
            labelId = label.Id;
        }

        Assert.That((await _service.SetLabelAsync(_cardId, _boardMemberId, labelId, true)).Status, Is.EqualTo(OperationStatus.Invalid));
    }

    [Test]
    public async Task AddComment_ReturnsTheTrimmedComment()
    {
        await _service.AddCommentAsync(_cardId, _boardMemberId, "First");
        var comment = await _service.AddCommentAsync(_cardId, _boardMemberId, "  Second ");

        Assert.That(comment.Value!.Text, Is.EqualTo("Second"));

        using var db = _db.CreateDbContext();
        Assert.That((await db.Comments.SingleAsync(x => x.Id == comment.Value.Id)).Text, Is.EqualTo("Second"));
        Assert.That((await _service.AddCommentAsync(_cardId, _boardMemberId, "  ")).Status, Is.EqualTo(OperationStatus.Invalid));
    }

    #endregion

    #region Subtasks

    [Test]
    public async Task Subtasks_EditUnchanged_LogsNothing()
    {
        var subtask = (await _service.AddSubtaskAsync(_cardId, _boardMemberId, "Write")).Value!;

        await _service.EditSubtaskAsync(_cardId, _boardMemberId, subtask.Id, "Write");
        await _service.SetSubtaskCompletedAsync(_cardId, _boardMemberId, subtask.Id, false);

        Assert.That(await ActivityCountAsync(_cardId), Is.EqualTo(1));
    }

    [Test]
    public async Task Subtasks_OfAnotherCard_NotFound()
    {
        var subtask = (await _service.AddSubtaskAsync(_otherCardId, _boardMemberId, "Theirs")).Value!;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _service.EditSubtaskAsync(_cardId, _boardMemberId, subtask.Id, "Mine")).Status, Is.EqualTo(OperationStatus.NotFound));
            Assert.That((await _service.DeleteSubtaskAsync(_cardId, _boardMemberId, subtask.Id)).Status, Is.EqualTo(OperationStatus.NotFound));
        });
    }

    [Test]
    public async Task Subtasks_MoveAndDelete_KeepIndicesInOrder()
    {
        var ids = new List<int>();

        foreach (var text in new[] { "A", "B", "C" })
            ids.Add((await _service.AddSubtaskAsync(_cardId, _boardMemberId, text)).Value!.Id);

        await _service.MoveSubtaskAsync(_cardId, _boardMemberId, ids[2], ids[0]);
        await _service.DeleteSubtaskAsync(_cardId, _boardMemberId, ids[0]);

        using var db = _db.CreateDbContext();
        var subtasks = await db.Subtasks.OrderBy(x => x.Index).Select(x => new { x.Text, x.Index }).ToListAsync();

        Assert.That(subtasks.Select(x => x.Text), Is.EqualTo(new[] { "C", "B" }));
        Assert.That(subtasks.Select(x => x.Index), Is.EqualTo(new[] { 0, 1 }));
    }

    #endregion

    #region Time tracking and links

    [Test]
    public async Task Timer_StartTwice_KeepsOneAndStopWithoutTimerConflicts()
    {
        var first = await _service.StartTimerAsync(_cardId, _boardMemberId);
        var second = await _service.StartTimerAsync(_cardId, _boardMemberId);

        Assert.That(second.Value!.Id, Is.EqualTo(first.Value!.Id));
        Assert.That((await _service.StopTimerAsync(_cardId, _boardMemberId)).Succeeded, Is.True);
        Assert.That((await _service.StopTimerAsync(_cardId, _boardMemberId)).Status, Is.EqualTo(OperationStatus.Conflict));
    }

    [Test]
    public async Task StopTimer_AfterLosingAccess_StillStopsOwnTimer()
    {
        await _service.StartTimerAsync(_cardId, _boardMemberId);

        using (var db = _db.CreateDbContext())
            await db.BoardMemberships.Where(x => x.UserId == _boardMemberId).ExecuteDeleteAsync();

        Assert.That(await _service.CanAccessCardAsync(_cardId, _boardMemberId), Is.False);
        Assert.That((await _service.StopTimerAsync(_cardId, _boardMemberId)).Succeeded, Is.True);

        using var check = _db.CreateDbContext();
        Assert.That(await check.TimeRecords.AnyAsync(x => x.EndedAt == null), Is.False);
    }

    [Test]
    public async Task TimeRecords_RunningOrEmpty_Refused()
    {
        var running = (await _service.StartTimerAsync(_cardId, _boardMemberId)).Value!;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _service.EditTimeRecordAsync(_cardId, _boardMemberId, running.Id, TimeSpan.FromHours(1))).Status, Is.EqualTo(OperationStatus.Conflict));
            Assert.That((await _service.AddTimeRecordAsync(_cardId, _boardMemberId, TimeSpan.Zero)).Status, Is.EqualTo(OperationStatus.Invalid));
            Assert.That((await _service.AddTimeRecordAsync(_cardId, _boardMemberId, TimeSpan.FromDays(1_000_000))).Status, Is.EqualTo(OperationStatus.Invalid));
            Assert.That((await _service.AddTimeRecordAsync(_cardId, _boardMemberId, CardService.MaxDuration)).Succeeded, Is.True);
        });
    }

    [Test]
    public async Task LinkCards_CardYouCannotOpen_Invalid()
    {
        var (_, _, otherCardId) = await _db.AddOtherBoardAsync(_outsiderId);

        var result = await _service.LinkCardsAsync(_cardId, _boardMemberId, otherCardId, "relates to");

        using var db = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(await db.CardLinks.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task RemoveLink_RemovesOnlyItsOwnReverseLink()
    {
        // Link the third card first: the old code deleted the first link pointing back at the card,
        // which would then be the third card's reverse link instead of the other card's.
        Assert.That((await _service.LinkCardsAsync(_cardId, _boardMemberId, _thirdCardId, "relates to")).Succeeded, Is.True);
        var link = await _service.LinkCardsAsync(_cardId, _boardMemberId, _otherCardId, "is blocked by");

        await _service.RemoveLinkAsync(_cardId, _boardMemberId, link.Value!.Id);

        using var check = _db.CreateDbContext();
        var remaining = await check.CardLinks.Select(x => new { x.CardOneId, x.CardTwoId, x.Category }).ToListAsync();

        Assert.That(remaining, Has.Count.EqualTo(2));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _cardId && x.CardTwoId == _thirdCardId));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _thirdCardId && x.CardTwoId == _cardId && x.Category == "relates to"));
    }

    [Test]
    public async Task LinkCards_EitherCardOutOfReach_RefusedAndNothingLinked()
    {
        var (_, _, foreignCardId) = await _db.AddOtherBoardAsync(_outsiderId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _service.LinkCardsAsync(_cardId, _boardMemberId, foreignCardId, "relates to")).Succeeded, Is.False);
            Assert.That((await _service.LinkCardsAsync(foreignCardId, _boardMemberId, _cardId, "relates to")).Succeeded, Is.False);
            Assert.That((await _service.LinkCardsAsync(_cardId, _outsiderId, _otherCardId, "relates to")).Succeeded, Is.False);
        });

        using var db = _db.CreateDbContext();
        Assert.That(await db.CardLinks.CountAsync(), Is.Zero);
        Assert.That(await db.Activities.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task RemoveLink_OtherCardOnABoardTheUserCannotOpen_StillRemovesBothSides()
    {
        // The user can open the card they see the link on, so they can clean up a link to a board they lost access to.
        var (_, _, foreignCardId) = await _db.AddOtherBoardAsync(_outsiderId);
        int linkId;

        using (var db = _db.CreateDbContext())
        {
            var link = new CardLink { CardOneId = _cardId, CardTwoId = foreignCardId, Category = "relates to" };
            db.CardLinks.AddRange(link, new CardLink { CardOneId = foreignCardId, CardTwoId = _cardId, Category = "relates to" });
            await db.SaveChangesAsync();
            linkId = link.Id;
        }

        Assert.That((await _service.RemoveLinkAsync(_cardId, _boardMemberId, linkId)).Succeeded, Is.True);

        using var check = _db.CreateDbContext();
        Assert.That(await check.CardLinks.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Outsider_ChangingItemsOfACard_NotFoundAndNothingChanges()
    {
        var subtaskId = (await _service.AddSubtaskAsync(_cardId, _boardMemberId, "Subtask")).Value!.Id;
        var reminderId = (await _service.AddReminderAsync(_cardId, _boardMemberId, new DateTime(2030, 1, 2, 9, 0, 0))).Value!.Id;
        var recordId = (await _service.AddTimeRecordAsync(_cardId, _boardMemberId, TimeSpan.FromHours(1))).Value!.Id;
        var attachmentId = (await _service.AddAttachmentAsync(_cardId, _boardMemberId, "stored.txt", "x.txt")).Value!.Id;
        var linkId = (await _service.LinkCardsAsync(_cardId, _boardMemberId, _otherCardId, "relates to")).Value!.Id;
        var activitiesBefore = await ActivityCountAsync(_cardId);

        IOperationResult[] results =
        [
            await _service.EditSubtaskAsync(_cardId, _outsiderId, subtaskId, "Hijacked"),
            await _service.SetSubtaskCompletedAsync(_cardId, _outsiderId, subtaskId, true),
            await _service.MoveSubtaskAsync(_cardId, _outsiderId, subtaskId, null),
            await _service.SetSubtaskAssigneeAsync(_cardId, _outsiderId, subtaskId, _db.BoardMemberId, true),
            await _service.DeleteSubtaskAsync(_cardId, _outsiderId, subtaskId),
            await _service.EditTimeRecordAsync(_cardId, _outsiderId, recordId, TimeSpan.FromHours(9)),
            await _service.RemoveTimeRecordAsync(_cardId, _outsiderId, recordId),
            await _service.RemoveReminderAsync(_cardId, _outsiderId, reminderId),
            await _service.RemoveAttachmentAsync(_cardId, _outsiderId, attachmentId),
            await _service.RemoveLinkAsync(_cardId, _outsiderId, linkId),
        ];

        using var check = _db.CreateDbContext();
        var subtask = await check.Subtasks.Include(x => x.Assignees).SingleAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(results.Select(x => x.Status), Is.All.EqualTo(OperationStatus.NotFound));
            Assert.That(subtask.Text, Is.EqualTo("Subtask"));
            Assert.That(subtask.Completed, Is.False);
            Assert.That(subtask.Assignees, Is.Empty);
            Assert.That((await check.TimeRecords.SingleAsync()).GetTotalTime(), Is.EqualTo(TimeSpan.FromHours(1)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(await check.Reminders.CountAsync(), Is.EqualTo(1));
            Assert.That(await check.Attachments.CountAsync(), Is.EqualTo(1));
            Assert.That(await check.CardLinks.CountAsync(), Is.EqualTo(2));
            Assert.That(await ActivityCountAsync(_cardId), Is.EqualTo(activitiesBefore));
        });
    }

    [Test]
    public async Task ProjectMember_ChangingItemsOfACard_Works()
    {
        var subtaskId = (await _service.AddSubtaskAsync(_cardId, _projectMemberId, "Subtask")).Value!.Id;

        Assert.That((await _service.EditSubtaskAsync(_cardId, _projectMemberId, subtaskId, "Edited")).Succeeded, Is.True);

        var recordId = (await _service.AddTimeRecordAsync(_cardId, _projectMemberId, TimeSpan.FromHours(1))).Value!.Id;
        Assert.That((await _service.RemoveTimeRecordAsync(_cardId, _projectMemberId, recordId)).Succeeded, Is.True);

        using var check = _db.CreateDbContext();
        Assert.That((await check.Subtasks.SingleAsync()).Text, Is.EqualTo("Edited"));
        Assert.That(await check.TimeRecords.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task RemoveLink_OfAnotherCard_NotFound()
    {
        var link = await _service.LinkCardsAsync(_otherCardId, _boardMemberId, _thirdCardId, "relates to");

        Assert.That((await _service.RemoveLinkAsync(_cardId, _boardMemberId, link.Value!.Id)).Status, Is.EqualTo(OperationStatus.NotFound));
    }

    #endregion
}
