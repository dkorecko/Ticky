using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs CardService against an in-memory SQLite database.</summary>
public class CardServiceTest
{
    private SqliteConnection _connection = null!;
    private TestDbContextFactory _factory = null!;
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
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new TestDbContextFactory(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _service = new CardService(_factory, new CardNumberingService(_factory));

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        var boardMember = new User { DisplayName = "Board member", UserName = "board@test" };
        var projectMember = new User { DisplayName = "Project member", UserName = "project@test" };
        var outsider = new User { DisplayName = "Outsider", UserName = "outsider@test" };
        db.Users.AddRange(boardMember, projectMember, outsider);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        project.Memberships.Add(new ProjectMembership { UserId = projectMember.Id, ProjectId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        board.Memberships.Add(new BoardMembership { UserId = boardMember.Id, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var column = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        db.Columns.Add(column);
        await db.SaveChangesAsync();

        Card NewCard(int number) => new() { Name = $"Card {number}", Number = number, Index = number, ColumnId = column.Id, CreatedById = boardMember.Id };
        var card = NewCard(1);
        var otherCard = NewCard(2);
        var thirdCard = NewCard(3);
        db.Cards.AddRange(card, otherCard, thirdCard);
        await db.SaveChangesAsync();

        (_boardMemberId, _projectMemberId, _outsiderId) = (boardMember.Id, projectMember.Id, outsider.Id);
        (_cardId, _otherCardId, _thirdCardId) = (card.Id, otherCard.Id, thirdCard.Id);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

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

    [Test]
    public async Task RemoveLink_RemovesOnlyItsOwnReverseLink()
    {
        // Link the third card first: the old code deleted the first link pointing back at the card,
        // which would then be the third card's reverse link instead of the other card's.
        Assert.That(await _service.LinkCardsAsync(_cardId, _thirdCardId, _boardMemberId, "relates to"), Is.Null);
        Assert.That(await _service.LinkCardsAsync(_cardId, _otherCardId, _boardMemberId, "is blocked by"), Is.Null);

        using (var db = _factory.CreateDbContext())
        {
            var link = await db.CardLinks.SingleAsync(x => x.CardOneId == _cardId && x.CardTwoId == _otherCardId);
            await _service.RemoveLinkAsync(link.Id, _boardMemberId);
        }

        using var check = _factory.CreateDbContext();
        var remaining = await check.CardLinks.Select(x => new { x.CardOneId, x.CardTwoId, x.Category }).ToListAsync();

        Assert.That(remaining, Has.Count.EqualTo(2));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _cardId && x.CardTwoId == _thirdCardId));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _thirdCardId && x.CardTwoId == _cardId && x.Category == "relates to"));
    }

    [Test]
    public async Task Activity_StoresRawValueAndEscapesWhenShown()
    {
        await _service.AddSubtaskAsync(_cardId, _boardMemberId, "<img src=x onerror=alert(1)>");

        using var db = _factory.CreateDbContext();
        var activity = (SubtaskAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.SubtaskAdded));
            Assert.That(activity.LegacyText, Is.Null);
            Assert.That(activity.SubtaskText, Is.EqualTo("<img src=x onerror=alert(1)>"));
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>added</b> subtask <b>&lt;img src=x onerror=alert(1)&gt;</b>"));
        });
    }

    [Test]
    public async Task Rename_KeepsOldAndNewTitle()
    {
        await _service.RenameAsync(_cardId, _boardMemberId, "Renamed");

        using var db = _factory.CreateDbContext();
        var activity = (TitleChangedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.ToHtml(), Is.EqualTo("<b>changed</b> the title from <b>Card 1</b> to <b>Renamed</b>"));
    }

    [Test]
    public async Task Join_IsShownAsJoining()
    {
        await _service.JoinAsync(_cardId, _boardMemberId);

        using var db = _factory.CreateDbContext();
        var activity = (AssigneeAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.AssigneeName, Is.EqualTo("Board member"));
        Assert.That(activity.ToHtml(), Is.EqualTo("<b>joined</b> the card"));
    }

    private async Task<int> AddColumnAsync(string name, bool finished, int maxCards = 0)
    {
        using var db = _factory.CreateDbContext();
        var boardId = (await db.Cards.Include(x => x.Column).SingleAsync(x => x.Id == _cardId)).Column.BoardId;
        var column = new Column { Name = name, BoardId = boardId, Index = 1, Finished = finished, MaxCards = maxCards };
        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column.Id;
    }

    [Test]
    public async Task MoveToColumn_RecordsTypedMoveWithColumnSnapshot()
    {
        var doneId = await AddColumnAsync("Done & dusted", finished: true);

        Assert.That(await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null), Is.True);

        using var db = _factory.CreateDbContext();
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
    public async Task MoveToColumn_PlacesBeforeTargetCardAndFixesIndices()
    {
        var doneId = await AddColumnAsync("Done", finished: true);

        await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null);
        await _service.MoveToColumnAsync(_otherCardId, _boardMemberId, doneId, _cardId);

        using var db = _factory.CreateDbContext();
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
    public async Task MoveToColumn_FullColumn_ReturnsFalseAndChangesNothing()
    {
        var doneId = await AddColumnAsync("Done", finished: true, maxCards: 1);
        await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null);

        Assert.That(await _service.MoveToColumnAsync(_otherCardId, _boardMemberId, doneId, null), Is.False);

        using var db = _factory.CreateDbContext();
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _otherCardId)).ColumnId, Is.Not.EqualTo(doneId));
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _otherCardId), Is.Zero);
    }

    [Test]
    public async Task CreateCard_ProjectMember_AddsTrimmedCardAtBottom()
    {
        var columnId = await AddColumnAsync("Doing", finished: false);
        await _service.CreateCardAsync(columnId, _projectMemberId, "First");
        var result = await _service.CreateCardAsync(columnId, _projectMemberId, "  Second  ");

        Assert.That(result.Status, Is.EqualTo(CardService.CreateCardStatus.Created));

        using var db = _factory.CreateDbContext();
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
        var columnId = await AddColumnAsync("Doing", finished: false);
        await _service.CreateCardAsync(columnId, _boardMemberId, "Older");

        using (var db = _factory.CreateDbContext())
        {
            (await db.Columns.SingleAsync(x => x.Id == columnId)).NewCardPlacement = CardPlacement.Top;
            await db.SaveChangesAsync();
        }

        await _service.CreateCardAsync(columnId, _boardMemberId, "Newer");

        using var check = _factory.CreateDbContext();
        var names = await check.Cards.Where(x => x.ColumnId == columnId).OrderBy(x => x.Index).Select(x => x.Name).ToListAsync();
        Assert.That(names, Is.EqualTo(new[] { "Newer", "Older" }));
    }

    [Test]
    public async Task CreateCard_Outsider_NotFoundAndNothingCreated()
    {
        var columnId = await AddColumnAsync("Doing", finished: false);
        var result = await _service.CreateCardAsync(columnId, _outsiderId, "Sneaky");

        using var db = _factory.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(CardService.CreateCardStatus.NotFound));
        Assert.That(await db.Cards.CountAsync(x => x.ColumnId == columnId), Is.Zero);
    }

    [Test]
    public async Task CreateCard_FullColumn_ColumnFullAndNothingCreated()
    {
        var columnId = await AddColumnAsync("Done", finished: true, maxCards: 1);

        Assert.That((await _service.CreateCardAsync(columnId, _boardMemberId, "Fits")).Status, Is.EqualTo(CardService.CreateCardStatus.Created));
        Assert.That((await _service.CreateCardAsync(columnId, _boardMemberId, "Too many")).Status, Is.EqualTo(CardService.CreateCardStatus.ColumnFull));

        using var db = _factory.CreateDbContext();
        Assert.That(await db.Cards.CountAsync(x => x.ColumnId == columnId), Is.EqualTo(1));
    }

    [Test]
    public async Task CreateCard_BlankName_InvalidName()
    {
        var columnId = await AddColumnAsync("Doing", finished: false);
        Assert.That((await _service.CreateCardAsync(columnId, _boardMemberId, "   ")).Status, Is.EqualTo(CardService.CreateCardStatus.InvalidName));
    }

    [Test]
    public async Task CreateCard_AssignCreator_AssignsTheUser()
    {
        var columnId = await AddColumnAsync("Doing", finished: false);
        var result = await _service.CreateCardAsync(columnId, _boardMemberId, "Mine", assignCreator: true);

        using var db = _factory.CreateDbContext();
        var card = await db.Cards.Include(x => x.Assignees).SingleAsync(x => x.Id == result.Card!.Id);
        Assert.That(card.Assignees.Select(x => x.Id), Is.EqualTo(new[] { _boardMemberId }));
    }

    [Test]
    public async Task AddComment_ReturnsTheNewComment()
    {
        await _service.AddCommentAsync(_cardId, _boardMemberId, "First");
        var comment = await _service.AddCommentAsync(_cardId, _boardMemberId, "Second");

        Assert.That(comment, Is.Not.Null);
        Assert.That(comment!.Text, Is.EqualTo("Second"));

        using var db = _factory.CreateDbContext();
        Assert.That((await db.Comments.SingleAsync(x => x.Id == comment.Id)).Text, Is.EqualTo("Second"));
        Assert.That(await _service.AddCommentAsync(_cardId, _boardMemberId, "  "), Is.Null);
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

        using var db = _factory.CreateDbContext();

        Assert.That(await db.Boards.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 1 : 0));
        Assert.That(await db.Columns.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 1 : 0));
        Assert.That(await db.Cards.AccessibleBy(userId).CountAsync(), Is.EqualTo(canOpen ? 3 : 0));
    }

    [Test]
    public async Task Rename_SameName_LogsNothing()
    {
        await _service.RenameAsync(_cardId, _boardMemberId, "Card 1");

        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    [Test]
    public async Task SetDeadline_SameDeadline_KeepsReminderStateAndLogsOnce()
    {
        var deadline = new DateTime(2030, 1, 2, 9, 0, 0);
        await _service.SetDeadlineAsync(_cardId, _boardMemberId, deadline);

        using (var db = _factory.CreateDbContext())
        {
            (await db.Cards.SingleAsync(x => x.Id == _cardId)).DeadlineProcessed = true;
            await db.SaveChangesAsync();
        }

        await _service.SetDeadlineAsync(_cardId, _boardMemberId, deadline);

        using var check = _factory.CreateDbContext();
        Assert.That((await check.Cards.SingleAsync(x => x.Id == _cardId)).DeadlineProcessed, Is.True);
        Assert.That(await check.Activities.CountAsync(x => x.CardId == _cardId), Is.EqualTo(1));
    }

    [Test]
    public async Task RemoveDeadline_NoDeadline_LogsNothing()
    {
        await _service.RemoveDeadlineAsync(_cardId, _boardMemberId);

        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    [Test]
    public async Task SetFlagged_SetsAndClearsWithoutLogging()
    {
        await _service.SetFlaggedAsync(_cardId, true);

        using (var db = _factory.CreateDbContext())
            Assert.That((await db.Cards.SingleAsync(x => x.Id == _cardId)).Flagged, Is.True);

        await _service.SetFlaggedAsync(_cardId, false);

        using var check = _factory.CreateDbContext();
        Assert.That((await check.Cards.SingleAsync(x => x.Id == _cardId)).Flagged, Is.False);
        Assert.That(await check.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    private static RepeatInfo NewRepeat(int? targetColumnId) =>
        new()
        {
            Type = RepeatType.Daily,
            Time = new TimeOnly(9, 0),
            CardPlacement = CardPlacement.Top,
            TargetColumnId = targetColumnId,
        };

    /// <summary>A column on a second board, which none of the test users belongs to.</summary>
    private async Task<int> AddColumnOnOtherBoardAsync()
    {
        using var db = _factory.CreateDbContext();
        var board = new Board { Name = "Other", Description = "", Code = "OTH", ProjectId = (await db.Boards.FirstAsync()).ProjectId };
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var column = new Column { Name = "Victim column", BoardId = board.Id, Index = 0 };
        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column.Id;
    }

    private async Task<RepeatInfo?> StoredRepeatAsync()
    {
        using var db = _factory.CreateDbContext();
        return (await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo;
    }

    [Test]
    public async Task SetRepeat_ColumnOnTheCardsBoard_IsStored()
    {
        var doingId = await AddColumnAsync("Doing", finished: false);

        Assert.That(await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(doingId)), Is.True);

        Assert.That((await StoredRepeatAsync())!.TargetColumnId, Is.EqualTo(doingId));
        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.EqualTo(1));
    }

    [Test]
    public async Task SetRepeat_ColumnOnAnotherBoard_IsRejectedAndNothingChanges()
    {
        var foreignColumnId = await AddColumnOnOtherBoardAsync();

        Assert.That(await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(foreignColumnId)), Is.False);

        Assert.That(await StoredRepeatAsync(), Is.Null);
        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    [Test]
    public async Task SetRepeat_ForeignColumnOnCardWithRepeat_KeepsTheEarlierRepeat()
    {
        var doingId = await AddColumnAsync("Doing", finished: false);
        await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(doingId));

        Assert.That(await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(await AddColumnOnOtherBoardAsync())), Is.False);

        Assert.That((await StoredRepeatAsync())!.TargetColumnId, Is.EqualTo(doingId));
    }

    [TestCase(999_999)]
    [TestCase(0)]
    [TestCase(-1)]
    public async Task SetRepeat_ColumnThatDoesNotExist_IsRejected(int targetColumnId)
    {
        Assert.That(await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(targetColumnId)), Is.False);
        Assert.That(await StoredRepeatAsync(), Is.Null);
    }

    [Test]
    public async Task SetRepeat_UserWhoCannotOpenTheCard_ChangesNothing()
    {
        var doingId = await AddColumnAsync("Doing", finished: false);

        await _service.SetRepeatAsync(_cardId, _outsiderId, NewRepeat(doingId));
        await _service.SetRepeatAsync(_cardId, _outsiderId, NewRepeat(null));

        Assert.That(await StoredRepeatAsync(), Is.Null);
        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    [Test]
    public async Task SetRepeat_NoTarget_IsRejectedAndNothingChanges()
    {
        Assert.That(await _service.SetRepeatAsync(_cardId, _boardMemberId, NewRepeat(null)), Is.False);

        Assert.That(await StoredRepeatAsync(), Is.Null);
        using var db = _factory.CreateDbContext();
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _cardId), Is.Zero);
    }

    #region Access: users who can't open a card's board can't act on it or its items

    /// <summary>A card on a board that nobody from the fixture can open.</summary>
    private async Task<int> AddForeignCardAsync()
    {
        using var db = _factory.CreateDbContext();

        var project = new Project { Name = "Foreign project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Foreign", Description = "", Code = "FOR", ProjectId = project.Id };
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var column = new Column { Name = "Foreign column", BoardId = board.Id, Index = 0 };
        db.Columns.Add(column);
        await db.SaveChangesAsync();

        var card = new Card { Name = "Foreign card", Number = 1, Index = 0, ColumnId = column.Id, CreatedById = _boardMemberId };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        return card.Id;
    }

    [Test]
    public async Task Outsider_ChangingACard_ChangesNothing()
    {
        var doneId = await AddColumnAsync("Done", finished: true);

        using (var db = _factory.CreateDbContext())
        {
            var board = await db.Boards.SingleAsync();
            db.Labels.Add(new Label { Name = "Bug", BoardId = board.Id, TextColor = System.Drawing.Color.Black, BackgroundColor = System.Drawing.Color.Red });
            await db.SaveChangesAsync();
        }

        int labelId;
        using (var db = _factory.CreateDbContext())
            labelId = (await db.Labels.SingleAsync()).Id;

        var deadline = new DateTime(2030, 1, 2, 9, 0, 0);

        await _service.RenameAsync(_cardId, _outsiderId, "Hijacked");
        await _service.UpdateDescriptionAsync(_cardId, _outsiderId, "Hijacked");
        await _service.SetPriorityAsync(_cardId, _outsiderId, CardPriority.Critical);
        await _service.SetDeadlineAsync(_cardId, _outsiderId, deadline);
        await _service.SnoozeAsync(_cardId, _outsiderId, deadline);
        await _service.MoveToAdjacentColumnAsync(_cardId, _outsiderId, 1);
        await _service.JoinAsync(_cardId, _outsiderId);
        await _service.AddSubtaskAsync(_cardId, _outsiderId, "Sneaky");
        await _service.AddReminderAsync(_cardId, _outsiderId, deadline);
        await _service.StartTimerAsync(_cardId, _outsiderId);
        await _service.AddTimeRecordAsync(_cardId, _outsiderId, TimeSpan.FromHours(1));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.MoveToColumnAsync(_cardId, _outsiderId, doneId, null), Is.False);
            Assert.That(await _service.ToggleLabelAsync(_cardId, _outsiderId, labelId), Is.Null);
            Assert.That(await _service.ToggleAssigneeAsync(_cardId, _outsiderId, _boardMemberId), Is.Null);
            Assert.That(await _service.AddCommentAsync(_cardId, _outsiderId, "Hi"), Is.Null);
            Assert.That(await _service.AddAttachmentAsync(_cardId, _outsiderId, "stored.txt", "x.txt"), Is.False);
        });

        using var check = _factory.CreateDbContext();
        var card = await check.Cards.Include(x => x.Assignees).Include(x => x.Labels).SingleAsync(x => x.Id == _cardId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(card.Name, Is.EqualTo("Card 1"));
            Assert.That(card.Description, Is.Empty);
            Assert.That(card.Priority, Is.EqualTo(CardPriority.Normal));
            Assert.That(card.Deadline, Is.Null);
            Assert.That(card.SnoozedUntil, Is.Null);
            Assert.That(card.Assignees, Is.Empty);
            Assert.That(card.Labels, Is.Empty);
            Assert.That(await check.Activities.CountAsync(), Is.Zero);
            Assert.That(await check.Comments.CountAsync(), Is.Zero);
            Assert.That(await check.Subtasks.CountAsync(), Is.Zero);
            Assert.That(await check.Reminders.CountAsync(), Is.Zero);
            Assert.That(await check.Attachments.CountAsync(), Is.Zero);
            Assert.That(await check.TimeRecords.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task Outsider_ChangingItemsOfACard_ChangesNothing()
    {
        var deadline = new DateTime(2030, 1, 2, 9, 0, 0);

        await _service.AddSubtaskAsync(_cardId, _boardMemberId, "Subtask");
        await _service.AddReminderAsync(_cardId, _boardMemberId, deadline);
        await _service.AddTimeRecordAsync(_cardId, _boardMemberId, TimeSpan.FromHours(1));
        await _service.AddAttachmentAsync(_cardId, _boardMemberId, "stored.txt", "x.txt");
        Assert.That(await _service.LinkCardsAsync(_cardId, _otherCardId, _boardMemberId, "relates to"), Is.Null);

        int subtaskId, reminderId, recordId, attachmentId, linkId;
        using (var db = _factory.CreateDbContext())
        {
            subtaskId = (await db.Subtasks.SingleAsync()).Id;
            reminderId = (await db.Reminders.SingleAsync()).Id;
            recordId = (await db.TimeRecords.SingleAsync()).Id;
            attachmentId = (await db.Attachments.SingleAsync()).Id;
            linkId = (await db.CardLinks.FirstAsync(x => x.CardOneId == _cardId)).Id;
        }

        int activitiesBefore;
        using (var db = _factory.CreateDbContext())
            activitiesBefore = await db.Activities.CountAsync();

        await _service.EditSubtaskAsync(subtaskId, _outsiderId, "Hijacked");
        await _service.SetSubtaskCompletedAsync(subtaskId, _outsiderId, true);
        await _service.DeleteSubtaskAsync(subtaskId, _outsiderId);
        await _service.EditTimeRecordAsync(recordId, _outsiderId, TimeSpan.FromHours(9));
        await _service.RemoveTimeRecordAsync(_cardId, _outsiderId, recordId);
        await _service.RemoveReminderAsync(_cardId, _outsiderId, reminderId);
        await _service.RemoveAttachmentAsync(_cardId, _outsiderId, attachmentId);
        await _service.RemoveLinkAsync(linkId, _outsiderId);

        Assert.That(await _service.ToggleSubtaskAssigneeAsync(subtaskId, _outsiderId, _boardMemberId), Is.Null);

        using var check = _factory.CreateDbContext();
        var subtask = await check.Subtasks.Include(x => x.Assignees).SingleAsync();
        var record = await check.TimeRecords.SingleAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(subtask.Text, Is.EqualTo("Subtask"));
            Assert.That(subtask.Completed, Is.False);
            Assert.That(subtask.Assignees, Is.Empty);
            Assert.That(record.GetTotalTime(), Is.EqualTo(TimeSpan.FromHours(1)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(await check.Reminders.CountAsync(), Is.EqualTo(1));
            Assert.That(await check.Attachments.CountAsync(), Is.EqualTo(1));
            Assert.That(await check.CardLinks.CountAsync(), Is.EqualTo(2));
            Assert.That(await check.Activities.CountAsync(), Is.EqualTo(activitiesBefore));
        });
    }

    [Test]
    public async Task ProjectMember_ChangingItemsOfACard_Works()
    {
        await _service.AddSubtaskAsync(_cardId, _projectMemberId, "Subtask");

        int subtaskId;
        using (var db = _factory.CreateDbContext())
            subtaskId = (await db.Subtasks.SingleAsync()).Id;

        await _service.EditSubtaskAsync(subtaskId, _projectMemberId, "Edited");
        await _service.AddTimeRecordAsync(_cardId, _projectMemberId, TimeSpan.FromHours(1));

        using var db2 = _factory.CreateDbContext();
        Assert.That((await db2.Subtasks.SingleAsync()).Text, Is.EqualTo("Edited"));

        var recordId = (await db2.TimeRecords.SingleAsync()).Id;
        await _service.RemoveTimeRecordAsync(_cardId, _projectMemberId, recordId);

        using var check = _factory.CreateDbContext();
        Assert.That(await check.TimeRecords.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task LinkCards_CardTheUserCannotOpen_IsRefusedAndNothingIsLinked()
    {
        var foreignCardId = await AddForeignCardAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.LinkCardsAsync(_cardId, foreignCardId, _boardMemberId, "relates to"), Is.Not.Null);
            Assert.That(await _service.LinkCardsAsync(foreignCardId, _cardId, _boardMemberId, "relates to"), Is.Not.Null);
            Assert.That(await _service.LinkCardsAsync(_cardId, _otherCardId, _outsiderId, "relates to"), Is.Not.Null);
        });

        using var db = _factory.CreateDbContext();
        Assert.That(await db.CardLinks.CountAsync(), Is.Zero);
        Assert.That(await db.Activities.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task RemoveLink_OtherCardOnABoardTheUserCannotOpen_StillRemovesBothSides()
    {
        // The user can open the card they see the link on, so they can clean up a link to a board they lost access to.
        var foreignCardId = await AddForeignCardAsync();

        using (var db = _factory.CreateDbContext())
        {
            db.CardLinks.Add(new CardLink { CardOneId = _cardId, CardTwoId = foreignCardId, Category = "relates to" });
            db.CardLinks.Add(new CardLink { CardOneId = foreignCardId, CardTwoId = _cardId, Category = "relates to" });
            await db.SaveChangesAsync();
        }

        int linkId;
        using (var db = _factory.CreateDbContext())
            linkId = (await db.CardLinks.SingleAsync(x => x.CardOneId == _cardId)).Id;

        await _service.RemoveLinkAsync(linkId, _boardMemberId);

        using var check = _factory.CreateDbContext();
        Assert.That(await check.CardLinks.CountAsync(), Is.Zero);
    }

    #endregion

    private class TestDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
