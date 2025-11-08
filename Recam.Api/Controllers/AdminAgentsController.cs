using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/agents")]
[Produces("application/json")]
public class AdminAgentsController : ControllerBase
{
    private readonly IAgentAdminService _svc;
    private readonly IListingCaseRepository  _listingCaseRepository;
    public AdminAgentsController(IAgentAdminService svc,  IListingCaseRepository  listingCaseRepository)
    {
        _svc = svc;
        _listingCaseRepository = listingCaseRepository;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateAgentResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CreateAgentResponse>>> CreateAgent(
        [FromBody] CreateAgentRequest req,
        CancellationToken ct)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var created = await _svc.CreateAgentAsync(adminUserId, req, ct);
        return Created(string.Empty, ApiResponse<CreateAgentResponse>.Success(created, "Agent created"));
    }
    
    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}/_debug-state")]
    [ApiExplorerSettings(IgnoreApi = false)] // 可选：不出现在正式文档里
    public async Task<ActionResult<ApiResponse<object>>> GetDebugState(int id, CancellationToken ct)
    {
        var dbg = await _listingCaseRepository.GetDebugStateAsync(id, ct);
        if (dbg is null)
            return NotFound(ApiResponse<object>.Fail("Listing not found."));

        var (lid, status, isDeleted) = dbg.Value;
        return Ok(ApiResponse<object>.Success(new
        {
            id = lid,
            status = status.ToString(),
            isDeleted
        }, "ok"));
    }
}