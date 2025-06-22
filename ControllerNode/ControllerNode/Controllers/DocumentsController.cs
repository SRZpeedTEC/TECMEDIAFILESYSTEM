// ControllerNode/Controllers/DocumentsController.cs
using ControllerNode.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ControllerNode.Controllers;

[ApiController]
public class DocumentsController : ControllerBase
{
    private readonly ControllerService _svc;
    public DocumentsController(ControllerService svc) => _svc = svc;

    [HttpGet("documents")]
    public IActionResult List([FromQuery] string? q = null)
        => Ok(_svc.ListDocuments(q));

    [HttpPost("documents")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file,
                                            CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        await _svc.AddDocumentAsync(file.FileName, ms.ToArray(), ct);
        return Ok();
    }

    [HttpGet("documents/{name}")]
    public async Task<IActionResult> Download(string name, CancellationToken ct)
    {
        var bytes = await _svc.GetDocumentAsync(name, ct);
        return bytes is null ? NotFound()
                             : File(bytes, "application/pdf", name);
    }

    [HttpDelete("documents/{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        await _svc.RemoveDocumentAsync(name, ct);
        return NoContent();
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(_svc.GetRaidStatus());
}
