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
    public async Task Download(string name, CancellationToken ct)
    {
        // 1) Validar existencia
        if (!_svc.Exists(name))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 2) Cabeceras HTTP
        Response.ContentType = "application/pdf";
        Response.ContentLength = _svc.GetFileSize(name);

        // 3) Stream de bloques al cliente según se reconstruyen
        await foreach (var chunk in _svc.StreamDocumentAsync(name, ct))
        {
            await Response.Body.WriteAsync(chunk, ct);
            await Response.Body.FlushAsync(ct);
        }
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
