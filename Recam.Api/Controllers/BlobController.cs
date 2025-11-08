using Microsoft.AspNetCore.Mvc;
using Recam.Models.Entities;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class BlobController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;

    public BlobController(IBlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }
    
    [HttpPost("Upload")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        using var stream = file.OpenReadStream();
        var (url, contentType, size) = await _blobStorageService.UploadAsync(
            stream,
            file.ContentType,
            file.FileName,
            ct
        );

        return Ok(new
        {
            Url = url,
            ContentType = contentType,
            Size = size
        });
    }

    [HttpGet("Download")]
    public async Task<IActionResult> Download([FromQuery] string url, CancellationToken ct)
    {
        var (content, contentType, fileName) = await _blobStorageService.DownloadFileAsync(url, ct);
        return File(content, contentType, fileName);
    }

    [HttpDelete("Delete")]
    public async Task<IActionResult> Delete([FromQuery] string url, CancellationToken ct)
    {
        var deleted = await _blobStorageService.DeleteAsync(url, ct);
        return Ok(new { Deleted = deleted });
    }

    [HttpGet("SasUrl")]
    public IActionResult GetSasUrl([FromQuery] string url, [FromQuery] int? minutes = null)
    {
        var sasUrl = _blobStorageService.GetReadOnlySasUrl(url, minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : null);
        return Ok(new { SasUrl = sasUrl });
    }

}