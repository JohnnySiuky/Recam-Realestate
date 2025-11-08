using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/companies")]
[Produces("application/json")]
public class PhotographyCompaniesController : ControllerBase
{
    private readonly IPhotographyCompanyService _service;

    public PhotographyCompaniesController(IPhotographyCompanyService service)
    {
        _service = service;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PhotographyCompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PhotographyCompanyListItemDto>>>> GetPhotographyCompanies(
        CancellationToken ct)
    {
        var item = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<PhotographyCompanyListItemDto>>.Success(item));
    }
    
    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpPost("agents")]
    [ProducesResponseType(typeof(ApiResponse<AddAgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AddAgentResponse>>> AddAgent(
        [FromBody] AddAgentRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value).ToArray();

        var result = await _service.AddAgentAsync(userId, roles, req.AgentEmail, req.CompanyId, ct);
        return Ok(ApiResponse<AddAgentResponse>.Success(result, "Agent added to company"));
    }
    
    [Authorize(Roles = "PhotographyCompany")]
    [HttpPost("me/agents")]
    [ProducesResponseType(typeof(ApiResponse<AddAgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AddAgentResponse>>> AddAgentToMyCompany(
        [FromBody] AddAgentByEmailRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.AgentEmail))
            return BadRequest(ApiResponse<object>.Fail("agentEmail is required", "BAD_REQUEST"));

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        // 傳 null 給 companyId，Service 會用 currentUserId 當目標公司
        var result = await _service.AddAgentAsync(currentUserId, roles, req.AgentEmail, companyId: null, ct);
        return Ok(ApiResponse<AddAgentResponse>.Success(result, "Agent added to your company"));
    }
    
    [Authorize(Roles = "PhotographyCompany")]
    [HttpGet("me/agents")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AgentListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentListItemDto>>>> GetMyAgents(CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var list = await _service.GetMyAgentsAsync(currentUserId, ct);
        return Ok(ApiResponse<IReadOnlyList<AgentListItemDto>>.Success(list, "ok"));
    }
}