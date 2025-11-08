using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Models.Enums;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;
public class UploadMediaForm
{
    [FromForm(Name = "type")]
    public MediaType Type { get; set; }   // Picture=0, Video=1, FloorPlan=2, VRTour=3

    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = new();
}

[Route("api/media")]
[ApiController]
[Produces("application/json")]
public class MediaController : ControllerBase
{   
    
    private readonly IMediaService _svc;
    private readonly IMediaAssetService _mediaAssetService;

    public MediaController(IMediaService svc, IMediaAssetService mediaAssetService)
    {
        _svc = svc;
        _mediaAssetService = mediaAssetService;
    }
    
    [Authorize(Roles = "Admin,PhotographyCompany")]
    [HttpPost("{id:int}/media")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UploadMediaAssetsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UploadMediaAssetsResponse>>> UploadMediaAssets(
        int id,
        [FromForm] UploadMediaForm form,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var resp = await _mediaAssetService.UploadAsync(userId, roles, id, form.Type, form.Files, ct);
        return Ok(ApiResponse<UploadMediaAssetsResponse>.Success(resp, "Uploaded"));
    }
    
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var (stream, contentType, fileName) = await _mediaAssetService.DownloadAsync(id, userId, roles, ct);
        return File(stream, contentType, fileName);
    }

    /// <summary>（可选）获取只读 SAS 下载链接</summary>
    [Authorize(Roles = "Admin,Agent,PhotographyCompany")]
    [HttpGet("{id:int}/sas")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetSas(int id, [FromQuery] int minutes = 15, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var url = await _mediaAssetService.GetDownloadSasAsync(id, userId, roles, minutes, ct);
        return Ok(ApiResponse<object>.Success(new { url }, "SAS generated"));
    }
    
    // Admin 刪除mdia（軟刪
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var changed = await _svc.DeleteAsync(id, userId, ct);
        var msg = changed ? "Media deleted." : "Media already deleted.";
        return Ok(ApiResponse<object>.Success(new { id }, msg));
    }
}