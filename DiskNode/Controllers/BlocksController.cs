using DiskNode.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlTypes;

namespace DiskNode.Controllers
{

    [ApiController]     //  Key attribute to indicate this is an API controller
    [Route("blocks")]
    public class BlocksController : ControllerBase
    {
        private readonly BlockStorage storage;

        public BlocksController(BlockStorage _storage)
        {
            storage = _storage;
        }

        [HttpPut("{index:long}")]        // Match PUT requests with a long index parameter
        public async Task<IActionResult> Put(long index)
        {
            var ContentLength = Request.ContentLength;        
            var expectedSize = storage.GetBlockSize();        

            if (ContentLength == null || ContentLength != expectedSize)
            {
                return BadRequest($"The size of the block has to be {expectedSize} KB.");  // Validate content length against expected block size
            }

            await storage.WriteAsync(index, Request.Body, HttpContext.RequestAborted);

            return NoContent();
        }

        [HttpGet("{index:long}")]      // Match GET requests with a long index parameter
        public async Task<IActionResult> Get(long index)
        {
            var stream = await storage.ReadAsync(index, HttpContext.RequestAborted);

            return File(stream, "application/octet-stream", enableRangeProcessing: false);
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok("OK");        // Health check endpoint to verify service status
        }


        [HttpDelete("{index:long}")]
        public async Task<IActionResult> Delete(long index)
        {
            await storage.DeleteAsync(index, HttpContext.RequestAborted);
            return NoContent();
        }


    }
}
