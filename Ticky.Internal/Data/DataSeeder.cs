using System.Drawing;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Ticky.Internal.Data;

public class DataSeeder
{
    public static async Task Seed(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var dataContext = serviceProvider.GetRequiredService<DataContext>();
        var avatarService = serviceProvider.GetRequiredService<AvatarService>();

        var adminClaim = new Claim(ClaimTypes.Role, Constants.Roles.Admin);
        var adminUsers = await userManager.GetUsersForClaimAsync(adminClaim);

        if (!adminUsers.Any())
        {
            var adminUser = new User
            {
                DisplayName = "Default Admin",
                UserName = Constants.Defaults.ADMIN_EMAIL,
                Email = Constants.Defaults.ADMIN_EMAIL,
                EmailConfirmed = true,
                NeedsNewCredentials = true
            };

            adminUser.ProfilePictureFileName = await avatarService.FetchAvatarAsync(
                adminUser.DisplayName
            );

            var result = await userManager.CreateAsync(
                adminUser,
                Constants.Defaults.ADMIN_PASSWORD
            );

            if (!result.Succeeded)
                throw new Exception(
                    $"Failed to create admin account. Errors:\n- {string.Join("\n- ", result.Errors)}"
                );

            result = await userManager.AddClaimAsync(adminUser, adminClaim);

            if (!result.Succeeded)
                throw new Exception(
                    $"Failed to add claims to the admin account. Errors:\n- {string.Join("\n- ", result.Errors)}"
                );
        }

        if (await dataContext.Users.AnyAsync())
            return;

#if DEBUG
        var testUser = new User
        {
            DisplayName = "Testing User",
            UserName = "user@ticky.com",
            Email = "user@ticky.com",
            EmailConfirmed = true,
            AutomaticDeadlineReminder = true
        };

        testUser.ProfilePictureFileName = await avatarService.FetchAvatarAsync(
            testUser.DisplayName
        );

        await userManager.CreateAsync(testUser, "abc123");

        var testProject = new Project { Name = "DAZN" };

        var projectMembership = new ProjectMembership
        {
            IsAdmin = true,
            ProjectId = testProject.Id,
            UserId = testUser.Id,
            AddedAt = DateTime.Now
        };

        testProject.Memberships.Add(projectMembership);

        var testBoard = new Board
        {
            ProjectId = testProject.Id,
            Code = "TB",
            Name = "Testing board",
            Description = "This is an example of a testing board with a description."
        };

        testProject.Boards.Add(testBoard);

        var label = new Label
        {
            Text = "Important",
            BoardId = testBoard.Id,
            BackgroundColor = Color.FromArgb(255, 251, 207, 232),
            TextColor = Color.FromArgb(255, 157, 23, 77)
        };

        testBoard.Labels = [label];

        var boardMembership = new BoardMembership
        {
            IsAdmin = true,
            BoardId = testBoard.Id,
            UserId = testUser.Id,
            AddedAt = DateTime.Now
        };

        var readyColumn = new Column
        {
            BoardId = testBoard.Id,
            Name = "Ready",
            Index = 0
        };

        var inProgressColumn = new Column
        {
            BoardId = testBoard.Id,
            Name = "WIP",
            Index = 1,
            MaxCards = 1
        };

        var doneColumn = new Column
        {
            BoardId = testBoard.Id,
            Name = "Done",
            Index = 2,
            Finished = true
        };

        testBoard.Columns.Add(readyColumn);
        testBoard.Columns.Add(inProgressColumn);
        testBoard.Columns.Add(doneColumn);

        var testCard = new Card
        {
            Text =
                "This is a pretty long example task, just trying out the word wrapping and stuff.",
            Description = "AC:",
            Number = 1,
            Index = 0,
            CreatedAt = DateTime.Now,
            Deadline = DateTime.Today.AddDays(5),
            ColumnId = readyColumn.Id,
            CreatedById = testUser.Id
        };

        readyColumn.Cards.Add(testCard);

        testCard.Labels.Add(label);

        testCard.Assignees = new List<User> { testUser };

        var subtask1 = new Subtask
        {
            CardId = testCard.Id,
            Index = 0,
            Text = "This is the first sub-task",
            Completed = true
        };

        var subtask2 = new Subtask
        {
            CardId = testCard.Id,
            Index = 1,
            Text = "This is the second sub-task",
            Completed = true
        };

        var subtask3 = new Subtask
        {
            CardId = testCard.Id,
            Index = 1,
            Text = "This is the third sub-task"
        };

        testCard.Subtasks = new List<Subtask> { subtask1, subtask2, subtask3 };

        var timeRecord = new TimeRecord
        {
            CardId = testCard.Id,
            UserId = testUser.Id,
            StartedAt = DateTime.Now.AddHours(-1).AddMinutes(-19),
            EndedAt = DateTime.Now
        };

        testCard.TimeRecords.Add(timeRecord);

        dataContext.Projects.Add(testProject);

        await dataContext.SaveChangesAsync();
#endif
    }
}
