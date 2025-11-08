using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Models.Enums;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;
using Recam.Services.Logging.interfaces;

namespace PMS.API.Controllers;


[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public class ListingsController : ControllerBase
{
    private readonly IListingCaseService _svc;
    private readonly IMediaAssetService _mediaAssetService;
    private readonly IFinalSelectionService _finalSelectionSvc;
    private readonly IMediaSelectionLogService _mediaSelectionLog;


    public ListingsController(IListingCaseService svc, IMediaAssetService mediaAssetService, IFinalSelectionService finalSelectionSvc,  IMediaSelectionLogService mediaSelectionLog)
    {
        _svc = svc;
        _mediaAssetService = mediaAssetService;
        _finalSelectionSvc = finalSelectionSvc;
        _mediaSelectionLog = mediaSelectionLog;
    } 
    

    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ListingCaseDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ListingCaseDto>>> Create(
        [FromBody] CreateListingCaseRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canCreate = User.IsInRole("Admin") || User.IsInRole("PhotographyCompany");

        var result = await _svc.CreateAsync(userId, canCreate, req, ct);
        return Created(string.Empty, ApiResponse<ListingCaseDto>.Success(result, "Created"));
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ListingCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ListingCaseDto>>> Update(
        int id,
        [FromBody] UpdateListingCaseRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var dto = await _svc.UpdateAsync(id, userId, isAdmin: true, req, ct);
        return Ok(ApiResponse<ListingCaseDto>.Success(dto, "Updated"));
    }
    
    // GET /api/listings
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ListingCaseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ListingCaseDto>>>> GetAll(
        [FromQuery] GetListingCasesQuery query,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var paged = await _svc.GetPagedAsync(userId, roles, query, ct);

        var meta = new
        {
            page       = paged.Page,
            pageSize   = paged.PageSize,
            total      = paged.Total,
            totalPages = (int)Math.Ceiling(paged.Total / (double)paged.PageSize),
            sortBy     = query.SortBy,
            sortDir    = query.SortDir
        };

        return Ok(ApiResponse<IEnumerable<ListingCaseDto>>.Success(paged.Items, "ok", meta));
    }
    
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _svc.DeleteAsync(id, userId, isAdmin: true, ct);

        return Ok(ApiResponse<object>.Success(
            new { id },
            message: "Listing deleted (soft-deleted) successfully"));
    }
    
    
    // GET /api/listings/{id}
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ListingCaseDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ListingCaseDetailDto>>> GetById(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var dto = await _svc.GetDetailAsync(id, userId, roles, ct);
        return Ok(ApiResponse<ListingCaseDetailDto>.Success(dto, "ok"));
    }
    
    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<ChangeListingStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChangeListingStatusResponse>>> PatchStatus(
        int id,
        [FromBody] ChangeListingStatusRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var result = await _svc.ChangeStatusAsync(id, userId, roles, req.Status, req.Reason, ct);
        return Ok(ApiResponse<ChangeListingStatusResponse>.Success(result, "Status updated"));
    }
    
    // GET /api/listings/{id}/media
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet("{id:int}/media")]
    [ProducesResponseType(typeof(ApiResponse<ListingMediaResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ListingMediaResponse>>> GetMedia(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var resp = await _svc.GetMediaAsync(id, userId, roles, ct);
        return Ok(ApiResponse<ListingMediaResponse>.Success(resp, "ok"));
    }
    
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpPost("{id:int}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<CaseContactDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CaseContactDto>>> AddContact(
        int id,
        [FromBody] AddCaseContactRequest body,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var dto = await _svc.AddContactAsync(id, userId, roles, body, ct);
        return Created(string.Empty, ApiResponse<CaseContactDto>.Success(dto, "Contact added"));
    }
    
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadAllMediaZip(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var (zipStream, fileName) = await _mediaAssetService.DownloadZipAsync(id, userId, roles, ct);
        return File(zipStream, "application/zip", fileName);
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/cover-image")]
    [ProducesResponseType(typeof(ApiResponse<SetCoverImageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SetCoverImageResponse>>> SetCoverImage(
        int id,
        [FromBody] SetCoverImageRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var resp = await _svc.SetCoverImageAsync(id, req.MediaId, userId, ct);
        return Ok(ApiResponse<SetCoverImageResponse>.Success(resp, "Cover image updated"));
    }
    
    [Authorize(Roles = "Admin,Agent")]
    [HttpGet("{id:int}/final-selection")]
    [ProducesResponseType(typeof(ApiResponse<FinalSelectionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FinalSelectionResponse>>> GetFinalSelection(
        int id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var resp = await _finalSelectionSvc.GetAsync(id, userId, roles, ct);
        return Ok(ApiResponse<FinalSelectionResponse>.Success(resp, "ok"));
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPost("{id:int}/selected-media")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSelectedMedia(
        int id,
        [FromBody] UpdateSelectedMediaRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        await _finalSelectionSvc.SaveAgentSelectionAsync(
            listingId: id,
            agentUserId: userId,
            roles: roles,
            selectedMediaIds: req.MediaAssetIds,
            markFinal: req.MarkAsFinal,
            ct: ct);

        return Ok(ApiResponse<object>.Success(
            new { listingCaseId = id, count = req.MediaAssetIds.Count, req.MarkAsFinal },
            "Selection recorded"));
    }
    
    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(typeof(ApiResponse<PublishListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublishListingResponse>>> Publish(
        int id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        var resp = await _svc.PublishAsync(id, userId, roles, ct);

        return Ok(ApiResponse<PublishListingResponse>.Success(resp, "Listing published"));
    }
}