using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>A board's labels. Board admins create, change and delete them; every member puts them on cards.</summary>
[Route(Constants.Api.RoutePrefix + "/labels")]
[Tags("Labels")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class LabelsController(IDbContextFactory<DataContext> dbContextFactory, LabelService labelService) : ApiControllerBase
{
    /// <summary>Create a label</summary>
    /// <remarks>Adds a label to the board. Board admins only.</remarks>
    /// <param name="boardId">The board's id.</param>
    /// <param name="request">The label's name and colors.</param>
    /// <response code="201">The new label.</response>
    /// <response code="400">The name is blank, or a color isn't like #DC2626.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpPost("/" + Constants.Api.RoutePrefix + "/boards/{boardId:int}/labels")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<LabelDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LabelDTO>> Create(int boardId, [FromBody] CreateLabelRequest request)
    {
        var result = await labelService.CreateAsync(
            boardId,
            UserId,
            request.Name,
            ColorTranslator.FromHtml(request.TextColor),
            ColorTranslator.FromHtml(request.BackgroundColor)
        );

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardId);
        // A label has no endpoint of its own; it is part of its board.
        return CreatedAtAction(nameof(BoardsController.Get), "Boards", new { id = boardId }, result.Value!.ToDto());
    }

    /// <summary>Update a label</summary>
    /// <remarks>Changes only the fields you send. Cards with the label show the change. Board admins only.</remarks>
    /// <param name="id">The label's id.</param>
    /// <param name="request">The fields to change.</param>
    /// <response code="200">The label after the changes.</response>
    /// <response code="400">The name is blank, or a color isn't like #DC2626.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The label doesn't exist, or you can't open its board.</response>
    [HttpPatch("{id:int}")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LabelDTO>> Update(int id, [FromBody] UpdateLabelRequest request)
    {
        var result = await labelService.UpdateAsync(id, UserId, new()
        {
            Name = request.Name,
            TextColor = request.TextColor is null ? null : ColorTranslator.FromHtml(request.TextColor),
            BackgroundColor = request.BackgroundColor is null ? null : ColorTranslator.FromHtml(request.BackgroundColor),
        });

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(result.Value!.BoardId);
        return result.Value.ToDto();
    }

    /// <summary>Delete a label</summary>
    /// <remarks>Deletes the label, which takes it off every card. Board admins only.</remarks>
    /// <param name="id">The label's id.</param>
    /// <response code="204">The label is deleted.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The label doesn't exist, or you can't open its board.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int id)
    {
        using var db = dbContextFactory.CreateDbContext();
        var boardId = await db.Labels.Where(x => x.Id == id).Select(x => x.BoardId).FirstOrDefaultAsync();
        var result = await labelService.DeleteAsync(id, UserId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardId);
        return NoContent();
    }
}
