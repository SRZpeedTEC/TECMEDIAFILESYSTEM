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
    public IActionResult List([FromQuery] string? query = null) => Ok(_svc.ListDocuments(query));

    //Http desde la pdf app, para subir un doc

    [HttpPost("documents")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        using var memorytream = new MemoryStream();
        await file.CopyToAsync(memorytream, ct);
        await _svc.AddDocumentAsync(file.FileName, memorytream.ToArray(), ct);
        return Ok();
    }

    //Http desde la pdf app, para descargar un doc

    [HttpGet("documents/{name}")] 
    public async Task<IActionResult> Download(string name, CancellationToken ct)
    
    {
        var bytes = await _svc.GetDocumentAsync(name, ct);
        return bytes is null ? NotFound() : File(bytes, "application/pdf", name);    


    }

    //Http desde la pdf app, para borrar un doc

    [HttpDelete("documents/{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        await _svc.RemoveDocumentAsync(name, ct);
        return NoContent();
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(_svc.GetRaidStatus());
}
