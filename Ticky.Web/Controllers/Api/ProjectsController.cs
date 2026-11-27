using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Projects group boards. Their members can open all their boards; their admins rename or delete them, add boards and
/// manage their members.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/projects")]
[Tags("Projects")]
public class ProjectsController(
    IDbContextFactory<DataContext> dbContextFactory,
    ProjectService projectService,
    BoardService boardService,
    MembershipService membershipService
) : ApiControllerBase
{
    /// <summary>List projects</summary>
    /// <remarks>The projects you're a member of. Boards you only reach through a board membership aren't here.</remarks>
    /// <response code="200">The projects.</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectSummaryDTO>>> List()
    {
        using var db = dbContextFactory.CreateDbContext();

        var projects = await db.ProjectMemberships
            .Where(x => x.UserId == UserId)
            .OrderBy(x => x.Project.Name)
            .Select(x => new ProjectSummaryDTO(x.ProjectId, x.Project.Name, x.IsAdmin, x.Project.Boards.Count))
            .ToListAsync();

        return Ok(projects);
    }

    /// <summary>Get a project</summary>
    /// <remarks>The project with its boards and members.</remarks>
    /// <param name="id">The project's id.</param>
    /// <response code="200">The project.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    [HttpGet("{id:int}")]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDTO>> Get(int id)
    {
        var detail = await LoadDetailAsync(id);

        if (detail is null)
            return NotFound();

        return detail;
    }

    /// <summary>Create a project</summary>
    /// <remarks>Creates a project with you as its admin. Add boards to it next.</remarks>
    /// <response code="201">The new project.</response>
    /// <response code="400">The name is missing or blank.</response>
    [HttpPost]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProjectDetailDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDetailDTO>> Create([FromBody] CreateProjectRequest request)
    {
        var result = await projectService.CreateAsync(UserId, request.Name);

        if (!result.Succeeded)
            return Failure(result);

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, await LoadDetailAsync(result.Value.Id));
    }

    /// <summary>Rename a project</summary>
    /// <param name="id">The project's id.</param>
    /// <param name="request">The new name.</param>
    /// <response code="200">The project.</response>
    /// <response code="400">The name is missing or blank.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the project.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    [HttpPatch("{id:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDTO>> Update(int id, [FromBody] UpdateProjectRequest request)
    {
        var result = await projectService.RenameAsync(id, UserId, request.Name);

        if (!result.Succeeded)
            return Failure(result);

        return (await LoadDetailAsync(id))!;
    }

    /// <summary>Delete a project</summary>
    /// <remarks>Deletes the project with all its boards, columns and cards. This can't be undone.</remarks>
    /// <param name="id">The project's id.</param>
    /// <response code="204">The project is deleted.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the project.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    [HttpDelete("{id:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int id)
    {
        var boardIds = await BoardIdsAsync(id);
        var result = await projectService.DeleteAsync(id, UserId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardIds);
        return NoContent();
    }

    /// <summary>Create a board</summary>
    /// <remarks>
    /// Adds a board to the project, with the labels Bug, Feature and Enhancement. It has no columns yet: add them
    /// next. Project admins only.
    /// </remarks>
    /// <param name="id">The project's id.</param>
    /// <param name="request">The board's name, description and code.</param>
    /// <response code="201">The new board.</response>
    /// <response code="400">The name is blank, or the code isn't 1 to 5 upper-case letters.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the project.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    [HttpPost("{id:int}/boards")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BoardDetailDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BoardDetailDTO>> CreateBoard(int id, [FromBody] CreateBoardRequest request)
    {
        var result = await boardService.CreateAsync(id, UserId, new(request.Name, request.Description ?? string.Empty, request.Code));

        if (!result.Succeeded)
            return Failure(result);

        var board = await BoardsController.LoadDetailAsync(dbContextFactory, result.Value!.Id, UserId);
        return CreatedAtAction(nameof(BoardsController.Get), "Boards", new { id = board!.Id }, board);
    }

    #region Members

    /// <summary>List a project's members</summary>
    /// <remarks>The project's members, who can open all its boards. Admins first.</remarks>
    /// <param name="id">The project's id.</param>
    /// <response code="200">The members.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    [HttpGet("{id:int}/members")]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<MemberDTO>>> ListMembers(int id)
    {
        var detail = await LoadDetailAsync(id);

        return detail is null ? NotFound() : Ok(detail.Members);
    }

    /// <summary>Add a project member</summary>
    /// <remarks>
    /// Adds the person with this e-mail address as a member, which opens all the project's boards to them. Make them
    /// an admin afterwards if needed. Project admins only.
    /// </remarks>
    /// <param name="id">The project's id.</param>
    /// <param name="request">The person's e-mail address.</param>
    /// <response code="201">The new member.</response>
    /// <response code="400">There is no account with this e-mail address.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the project.</response>
    /// <response code="404">The project doesn't exist, or you aren't a member of it.</response>
    /// <response code="409">They're already a member.</response>
    [HttpPost("{id:int}/members")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound), ProblemResponse(StatusCodes.Status409Conflict)]
    [ProducesResponseType<MemberDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MemberDTO>> AddMember(int id, [FromBody] AddMemberRequest request)
    {
        var result = await membershipService.AddProjectMemberAsync(id, UserId, request.Email);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(await BoardIdsAsync(id));
        return CreatedAtAction(nameof(ListMembers), new { id }, result.Value!.ToDto(MembershipSource.Project));
    }

    /// <summary>Change a project member's role</summary>
    /// <remarks>Makes a member an admin of the project, or a member again. You can't change your own role.</remarks>
    /// <param name="id">The project's id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="request">The role.</param>
    /// <response code="200">The member.</response>
    /// <response code="403">The token is read-only, you aren't an admin of the project, or it's your own role.</response>
    /// <response code="404">The project doesn't exist, you aren't a member of it, or they aren't.</response>
    [HttpPatch("{id:int}/members/{userId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberDTO>> UpdateMember(int id, int userId, [FromBody] UpdateMemberRequest request)
    {
        var result = await membershipService.SetProjectAdminAsync(id, UserId, userId, request.IsAdmin);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(await BoardIdsAsync(id));
        var detail = await LoadDetailAsync(id);
        return detail!.Members.First(x => x.UserId == userId);
    }

    /// <summary>Remove a project member</summary>
    /// <remarks>
    /// Takes away their project membership, and with it their access to the project's boards, except boards they're a
    /// member of themselves. You can't remove yourself.
    /// </remarks>
    /// <param name="id">The project's id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <response code="204">They're no longer a member.</response>
    /// <response code="403">The token is read-only, you aren't an admin of the project, or it's yourself.</response>
    /// <response code="404">The project doesn't exist, you aren't a member of it, or they aren't.</response>
    [HttpDelete("{id:int}/members/{userId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveMember(int id, int userId)
    {
        var result = await membershipService.RemoveProjectMemberAsync(id, UserId, userId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(await BoardIdsAsync(id));
        return NoContent();
    }

    #endregion

    /// <summary>The project as the API shows it, or null if it doesn't exist or the caller isn't a member.</summary>
    private async Task<ProjectDetailDTO?> LoadDetailAsync(int id)
    {
        using var db = dbContextFactory.CreateDbContext();

        var project = await db.Projects
            .AsNoTracking()
            .Include(x => x.Memberships)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id && x.Memberships.Any(m => m.UserId == UserId));

        if (project is null)
            return null;

        var boards = await db.Boards
            .Where(x => x.ProjectId == id)
            .OrderBy(x => x.Name)
            .WithAccessDetails(UserId)
            .ToListAsync();

        return new ProjectDetailDTO(
            project.Id,
            project.Name,
            project.Memberships.First(x => x.UserId == UserId).IsAdmin,
            boards.Select(x => x.ToSummaryDto(UserId)).ToList(),
            project.ToMemberDtos()
        );
    }

    private async Task<List<int>> BoardIdsAsync(int projectId)
    {
        using var db = dbContextFactory.CreateDbContext();

        return await db.Boards.Where(x => x.ProjectId == projectId).Select(x => x.Id).ToListAsync();
    }
}
