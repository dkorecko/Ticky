using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>A board's columns. Board admins add, change, reorder and delete them.</summary>
[Route(Constants.Api.RoutePrefix + "/columns")]
[Tags("Columns")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class ColumnsController(IDbContextFactory<DataContext> dbContextFactory, ColumnService columnService) : ApiControllerBase
{
    /// <summary>Create a column</summary>
    /// <remarks>Adds a column after the board's last one. Board admins only.</remarks>
    /// <param name="boardId">The board's id.</param>
    /// <param name="request">The column's settings.</param>
    /// <response code="201">The new column.</response>
    /// <response code="400">The name is blank, or the card limit is negative.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpPost("/" + Constants.Api.RoutePrefix + "/boards/{boardId:int}/columns")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ColumnDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ColumnDTO>> Create(int boardId, [FromBody] CreateColumnRequest request)
    {
        var result = await columnService.CreateAsync(boardId, UserId, new(request.Name)
        {
            Finished = request.Finished,
            MaxCards = request.MaxCards,
            NewCardPlacement = request.NewCardPlacement,
        });

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardId);
        // A column has no endpoint of its own; it is part of its board.
        return CreatedAtAction(nameof(BoardsController.Get), "Boards", new { id = boardId }, await LoadAsync(result.Value!.Id));
    }

    /// <summary>Update a column</summary>
    /// <remarks>
    /// Changes only the fields you send. Lowering the card limit below the cards the column holds is allowed; it only
    /// stops new cards. Board admins only.
    /// </remarks>
    /// <param name="id">The column's id.</param>
    /// <param name="request">The fields to change.</param>
    /// <response code="200">The column after the changes.</response>
    /// <response code="400">The name is blank, or the card limit is negative.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The column doesn't exist, or you can't open its board.</response>
    [HttpPatch("{id:int}")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ColumnDTO>> Update(int id, [FromBody] UpdateColumnRequest request)
    {
        var result = await columnService.UpdateAsync(id, UserId, new()
        {
            Name = request.Name,
            Finished = request.Finished,
            MaxCards = request.MaxCards,
            NewCardPlacement = request.NewCardPlacement,
            Collapsed = request.Collapsed,
        });

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(result.Value!.BoardId);
        return await LoadAsync(id);
    }

    /// <summary>Move a column</summary>
    /// <remarks>Puts the column before another column of its board, or last. Board admins only.</remarks>
    /// <param name="id">The column's id.</param>
    /// <param name="request">Where to put it.</param>
    /// <response code="200">The board's columns in their new order.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The column doesn't exist, or you can't open its board.</response>
    [HttpPost("{id:int}/move")]
    public async Task<ActionResult<IEnumerable<ColumnDTO>>> Move(int id, [FromBody] MoveColumnRequest request)
    {
        var result = await columnService.MoveAsync(id, UserId, request.BeforeColumnId);

        if (!result.Succeeded)
            return Failure(result);

        using var db = dbContextFactory.CreateDbContext();
        var boardId = await db.Columns.Where(x => x.Id == id).Select(x => x.BoardId).FirstAsync();

        await NotifyBoardsAsync(boardId);
        return Ok(await db.Columns.Where(x => x.BoardId == boardId).OrderBy(x => x.Index).Select(ApiMappings.ColumnProjection).ToListAsync());
    }

    /// <summary>Delete a column</summary>
    /// <remarks>Deletes the column with all its cards. This can't be undone. Board admins only.</remarks>
    /// <param name="id">The column's id.</param>
    /// <response code="204">The column is deleted.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The column doesn't exist, or you can't open its board.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int id)
    {
        using var db = dbContextFactory.CreateDbContext();
        var boardId = await db.Columns.Where(x => x.Id == id).Select(x => x.BoardId).FirstOrDefaultAsync();
        var result = await columnService.DeleteAsync(id, UserId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardId);
        return NoContent();
    }

    private async Task<ColumnDTO> LoadAsync(int id)
    {
        using var db = dbContextFactory.CreateDbContext();

        return await db.Columns.Where(x => x.Id == id).Select(ApiMappings.ColumnProjection).FirstAsync();
    }
}
