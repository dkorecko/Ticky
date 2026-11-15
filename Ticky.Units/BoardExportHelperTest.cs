using System.Drawing;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace Ticky.Units;

public class BoardExportHelperTest
{
    private const string SecretMarker = "TOP-SECRET";

    /// <summary>The Identity and Ticky account fields that must never reach an export file.</summary>
    private static readonly string[] ForbiddenPropertyNames =
    [
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed",
        "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "EmailConfirmed",
        "NormalizedEmail", "NormalizedUserName", "UserName", "ApiTokens", "TokenHash", "EmailVerificationCode",
        "ProfilePictureFileName", "LastVisitedBoardId", "NeedsNewCredentials",
    ];

    /// <summary>A user with every account field filled in, so that anything leaking shows up in the JSON.</summary>
    private static User NewUser(int id, string displayName)
    {
        var user = new User
        {
            Id = id,
            DisplayName = displayName,
            UserName = $"{displayName.ToLower()}@ticky.test",
            NormalizedUserName = $"{SecretMarker}-NORMALIZED-USERNAME-{id}",
            Email = $"{displayName.ToLower()}@ticky.test",
            NormalizedEmail = $"{SecretMarker}-NORMALIZED-EMAIL-{id}",
            EmailConfirmed = true,
            PasswordHash = $"{SecretMarker}-HASH-{id}",
            SecurityStamp = $"{SecretMarker}-STAMP-{id}",
            ConcurrencyStamp = $"{SecretMarker}-CONCURRENCY-{id}",
            PhoneNumber = $"{SecretMarker}-PHONE-{id}",
            PhoneNumberConfirmed = true,
            TwoFactorEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddHours(1),
            LockoutEnabled = true,
            AccessFailedCount = 3,
            ProfilePictureFileName = $"{SecretMarker}-picture-{id}.png",
            LastVisitedBoardId = 99,
            NeedsNewCredentials = true,
        };

        user.ApiTokens.Add(new ApiToken { UserId = id, Name = "token", TokenHash = $"{SecretMarker}-TOKEN-{id}" });
        user.EmailVerificationCode = new Code { UserId = id, Value = $"{SecretMarker}-CODE-{id}", CodePurpose = CodePurpose.ForgottenPassword };

        return user;
    }

    /// <summary>
    /// A board with a user on every path the export loads: board members, project members, a card's creator and
    /// assignees, a comment's author, an activity's actor, a time record's user and a subtask's assignees.
    /// </summary>
    private static (Board Board, int UserReferences) NewBoard()
    {
        var owner = NewUser(1, "Owner");
        var member = NewUser(2, "Member");
        var assignee = NewUser(3, "Assignee");
        var commenter = NewUser(4, "Commenter");
        var actor = NewUser(5, "Actor");
        var timer = NewUser(6, "Timer");
        var subtaskAssignee = NewUser(7, "Subtasker");
        var projectMember = NewUser(8, "ProjectMember");

        var project = new Project { Name = "Project" };
        project.Memberships.Add(new ProjectMembership { UserId = projectMember.Id, User = projectMember, ProjectId = 0, IsAdmin = true, AddedAt = DateTime.Now });

        var board = new Board { Name = "Board", Description = "Description", Code = "EXP", ProjectId = 0, Project = project };
        board.Memberships.Add(new BoardMembership { UserId = owner.Id, User = owner, BoardId = 0, IsAdmin = true, AddedAt = DateTime.Now });
        board.Memberships.Add(new BoardMembership { UserId = member.Id, User = member, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });

        var label = new Label { Name = "Label", TextColor = Color.White, BackgroundColor = Color.Red, BoardId = 0, Board = board };
        board.Labels.Add(label);

        var column = new Column { Name = "Column", BoardId = 0, Index = 0, Board = board };
        board.Columns.Add(column);

        var card = new Card { Name = "Card", Number = 1, Index = 0, ColumnId = 0, Column = column, CreatedById = owner.Id, CreatedBy = owner };
        column.Cards.Add(card);
        card.Labels.Add(label);
        card.Assignees.Add(assignee);
        assignee.AssignedTo.Add(card);

        card.Comments.Add(new Comment { Text = "Comment", CreatedById = commenter.Id, CreatedBy = commenter, CardId = 0, Card = card });
        card.Activities.Add(new AssigneeAddedActivity { UserId = actor.Id, User = actor, CardId = 0, Card = card, AssigneeId = assignee.Id, AssigneeName = assignee.DisplayName });
        card.TimeRecords.Add(new TimeRecord { CardId = 0, Card = card, UserId = timer.Id, User = timer, StartedAt = DateTime.Now });

        var subtask = new Subtask { Index = 0, Text = "Subtask", CardId = 0, Card = card };
        subtask.Assignees.Add(subtaskAssignee);
        card.Subtasks.Add(subtask);

        return (board, UserReferences: 9);
    }

    /// <summary>Every JSON object with a <c>DisplayName</c> is a user: nothing else in the graph has one.</summary>
    private static List<JsonElement> FindUsers(JsonElement element)
    {
        var users = new List<JsonElement>();

        void Walk(JsonElement current)
        {
            switch (current.ValueKind)
            {
                case JsonValueKind.Object:
                    if (current.TryGetProperty("DisplayName", out _))
                        users.Add(current);

                    foreach (var property in current.EnumerateObject())
                        Walk(property.Value);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in current.EnumerateArray())
                        Walk(item);
                    break;
            }
        }

        Walk(element);
        return users;
    }

    [Test]
    public void Serialize_LeavesOutAccountSecretsOfEveryUser()
    {
        var (board, _) = NewBoard();

        var json = BoardExportHelper.Serialize(board);

        Assert.That(json, Does.Not.Contain(SecretMarker));

        foreach (var name in ForbiddenPropertyNames)
            Assert.That(json, Does.Not.Contain($"\"{name}\""), name);
    }

    [Test]
    public void Serialize_WritesOnlyIdNameAndEmailForUsersOnEveryPath()
    {
        var (board, userReferences) = NewBoard();

        using var document = JsonDocument.Parse(BoardExportHelper.Serialize(board));
        var users = FindUsers(document.RootElement);

        Assert.That(users, Has.Count.EqualTo(userReferences));

        foreach (var user in users)
        {
            var names = user.EnumerateObject().Select(x => x.Name).ToList();
            Assert.That(names, Is.EquivalentTo(BoardExportHelper.ExportedUserProperties), user.GetRawText());
        }

        var member = users.Single(x => x.GetProperty("Id").GetInt32() == 2);
        Assert.That(member.GetProperty("DisplayName").GetString(), Is.EqualTo("Member"));
        Assert.That(member.GetProperty("Email").GetString(), Is.EqualTo("member@ticky.test"));
    }

    [Test]
    public void Serialize_UserDeclaredAsIdentityBaseType_IsReducedToo()
    {
        var holder = new { Owner = (IdentityUser<int>)NewUser(1, "Owner") };

        var json = BoardExportHelper.Serialize(holder);

        Assert.That(json, Does.Not.Contain(SecretMarker));
        Assert.That(json, Does.Not.Contain("\"PasswordHash\""));
        Assert.That(json, Does.Contain("\"Email\":\"owner@ticky.test\""));
    }

    [Test]
    public void Serialize_KeepsTheRestOfTheBoard()
    {
        var (board, _) = NewBoard();

        using var document = JsonDocument.Parse(BoardExportHelper.Serialize(board));
        var root = document.RootElement;

        Assert.That(root.GetProperty("Name").GetString(), Is.EqualTo("Board"));
        Assert.That(root.GetProperty("Code").GetString(), Is.EqualTo("EXP"));
        Assert.That(root.GetProperty("Project").GetProperty("Name").GetString(), Is.EqualTo("Project"));
        Assert.That(root.GetProperty("Labels").EnumerateArray().Single().GetProperty("Name").GetString(), Is.EqualTo("Label"));
        Assert.That(root.GetProperty("Memberships").GetArrayLength(), Is.EqualTo(2));

        var column = root.GetProperty("Columns").EnumerateArray().Single();
        Assert.That(column.GetProperty("Name").GetString(), Is.EqualTo("Column"));

        var card = column.GetProperty("Cards").EnumerateArray().Single();
        Assert.That(card.GetProperty("Name").GetString(), Is.EqualTo("Card"));
        Assert.That(card.GetProperty("Labels").GetArrayLength(), Is.EqualTo(1));
        Assert.That(card.GetProperty("Assignees").GetArrayLength(), Is.EqualTo(1));
        Assert.That(card.GetProperty("Comments").EnumerateArray().Single().GetProperty("Text").GetString(), Is.EqualTo("Comment"));
        Assert.That(card.GetProperty("TimeRecords").GetArrayLength(), Is.EqualTo(1));
        Assert.That(card.GetProperty("Subtasks").EnumerateArray().Single().GetProperty("Text").GetString(), Is.EqualTo("Subtask"));
        Assert.That(card.GetProperty("Activities").EnumerateArray().Single().GetProperty("kind").GetString(), Is.EqualTo(nameof(ActivityType.AssigneeAdded)));
    }
}
