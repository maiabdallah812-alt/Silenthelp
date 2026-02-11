using Microsoft.AspNetCore.Http;
using SilentHelp.Services;

namespace SilentHelp.Services
{
    public class AudioStorageService : IAudioStorageService
    {
        private readonly IWebHostEnvironment _env;

        public AudioStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveAudioAsync(IFormFile file, Guid userId)
        {
            var uploadsFolder = Path.Combine(
                _env.ContentRootPath,
                "Uploads",
                "Audio",
                userId.ToString()
            );

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.webm";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // relative path
            return $"/api/audio/{userId}/{fileName}";
        }
    }
}

