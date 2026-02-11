using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SilentHelp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AudioController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public AudioController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("{userId}/{fileName}")]
        [Authorize]
        public IActionResult GetAudio(string userId, string fileName)
        {
            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Audio", userId, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "audio/webm");
        }
    }
}
