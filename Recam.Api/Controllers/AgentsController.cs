using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/agents")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly IAgentQueryService _svc;
    public AgentsController(IAgentQueryService svc) => _svc = svc;

    // 只允許 Admin
    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AgentListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AgentListItemDto>>>> GetAll(CancellationToken ct)
    {
        var agents = await _svc.GetAllAsync(ct);
        return Ok(ApiResponse<IEnumerable<AgentListItemDto>>.Success(agents, "ok"));
    }
    
    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<AgentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AgentListItemDto>>> Search([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.Fail("email is required", "BAD_REQUEST"));

        var dto = await _svc.FindByEmailExactAsync(email, ct);
        if (dto is null)
            return NotFound(ApiResponse<object>.Fail("Agent not found", "NOT_FOUND"));

        return Ok(ApiResponse<AgentListItemDto>.Success(dto, "ok"));
    }
}