using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;
using Ticky.Web.Hubs;

namespace Ticky.Units;

/// <summary>Runs UpdateHub against an in-memory SQLite database, with the SignalR parts mocked.</summary>
public class UpdateHubTest
{
    private SqliteConnection _connection = null!;
    private CountingDbContextFactory _factory = null!;

    private int _boardMemberId;
    private int _projectMemberId;
    private int _outsiderId;
    private int _boardId;
    private int _otherBoardId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new CountingDbContextFactory(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );

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
        var otherProject = new Project { Name = "Other project" };
        db.Projects.Add(otherProject);
        await db.SaveChangesAsync();
        var otherBoard = new Board { Name = "Other board", Description = "", Code = "OTH", ProjectId = otherProject.Id };
        db.Boards.AddRange(board, otherBoard);
        await db.SaveChangesAsync();

        (_boardMemberId, _projectMemberId, _outsiderId) = (boardMember.Id, projectMember.Id, outsider.Id);
        (_boardId, _otherBoardId) = (board.Id, otherBoard.Id);

        _factory.ResetCount();
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    [Test]
    public async Task BoardChange_FromAUserWithoutAccess_IsNotBroadcast()
    {
        var connection = NewConnection(_outsiderId);

        await connection.Hub.BoardChange(_boardId, Guid.NewGuid());

        Assert.That(connection.Broadcasts, Is.Empty);
    }

    [Test]
    public async Task BoardChange_AfterBeingRefusedTheSubscription_IsNotBroadcast()
    {
        var connection = NewConnection(_outsiderId);

        await connection.Hub.SubscribeBoard(_boardId);
        await connection.Hub.BoardChange(_boardId, Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(connection.Subscriptions, Is.Empty);
            Assert.That(connection.Broadcasts, Is.Empty);
        });
    }

    [Test]
    public async Task BoardChange_ForAnotherBoardThanTheSubscribedOne_IsOnlyBroadcastIfTheUserCanOpenIt()
    {
        var connection = NewConnection(_boardMemberId);

        await connection.Hub.SubscribeBoard(_boardId);
        await connection.Hub.BoardChange(_otherBoardId, Guid.NewGuid());

        Assert.That(connection.Broadcasts, Is.Empty);
    }

    [Test]
    public async Task BoardChange_ForASubscribedBoard_ReachesItsGroupWithoutQueryingTheDatabase()
    {
        var connection = NewConnection(_boardMemberId);
        var pageId = Guid.NewGuid();

        await connection.Hub.SubscribeBoard(_boardId);
        var queriesAfterSubscribing = _factory.Created;
        await connection.Hub.BoardChange(_boardId, pageId);
        await connection.Hub.BoardChange(_boardId, pageId);

        Assert.Multiple(() =>
        {
            Assert.That(connection.Subscriptions, Is.EqualTo(new[] { UpdateHub.BoardGroup(_boardId) }));
            Assert.That(connection.Broadcasts, Is.EqualTo(Enumerable.Repeat(UpdateHub.BoardGroup(_boardId), 2)));
            Assert.That(_factory.Created, Is.EqualTo(queriesAfterSubscribing));
        });
    }

    [Test]
    public async Task BoardChange_WithoutASubscription_IsCheckedOnceAndThenRemembered()
    {
        // What a client that reconnected without subscribing again looks like: a fresh connection that has access.
        var connection = NewConnection(_projectMemberId);

        await connection.Hub.BoardChange(_boardId, Guid.NewGuid());
        var queriesAfterFirstChange = _factory.Created;
        await connection.Hub.BoardChange(_boardId, Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(connection.Broadcasts, Has.Count.EqualTo(2));
            Assert.That(queriesAfterFirstChange, Is.EqualTo(1));
            Assert.That(_factory.Created, Is.EqualTo(queriesAfterFirstChange));
        });
    }

    [Test]
    public async Task BoardChange_DoesNotShareWhatWasVerifiedBetweenConnections()
    {
        var member = NewConnection(_boardMemberId);
        var outsider = NewConnection(_outsiderId);

        await member.Hub.SubscribeBoard(_boardId);
        await outsider.Hub.BoardChange(_boardId, Guid.NewGuid());

        Assert.That(outsider.Broadcasts, Is.Empty);
    }

    private HubConnection NewConnection(int userId) => new(_factory, userId);

    /// <summary>One client connection: a hub instance whose context, groups and clients record what happens.</summary>
    private sealed class HubConnection
    {
        public UpdateHub Hub { get; }
        public List<string> Subscriptions { get; } = [];
        public List<string> Broadcasts { get; } = [];

        public HubConnection(IDbContextFactory<DataContext> factory, int userId)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
            var items = new Dictionary<object, object?>();
            var connectionId = Guid.NewGuid().ToString();

            var context = new Mock<HubCallerContext>();
            context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(identity));
            context.SetupGet(x => x.Items).Returns(items);
            context.SetupGet(x => x.ConnectionId).Returns(connectionId);

            var groups = new Mock<IGroupManager>();
            groups
                .Setup(x => x.AddToGroupAsync(connectionId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, group, _) => Subscriptions.Add(group))
                .Returns(Task.CompletedTask);

            var clients = new Mock<IHubCallerClients>();
            clients
                .Setup(x => x.Group(It.IsAny<string>()))
                .Returns<string>(group =>
                {
                    var proxy = new Mock<IClientProxy>();
                    proxy
                        .Setup(x => x.SendCoreAsync(nameof(UpdateHub.BoardChange), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                        .Callback(() => Broadcasts.Add(group))
                        .Returns(Task.CompletedTask);
                    return proxy.Object;
                });

            Hub = new UpdateHub(factory)
            {
                Context = context.Object,
                Groups = groups.Object,
                Clients = clients.Object,
            };
        }
    }

    private sealed class CountingDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        /// <summary>How many contexts were handed out since the test set up its data.</summary>
        public int Created => _created - _createdBeforeTest;

        private int _created;
        private int _createdBeforeTest;

        public DataContext CreateDbContext()
        {
            _created++;
            return new DataContext(options);
        }

        public void ResetCount() => _createdBeforeTest = _created;
    }
}
