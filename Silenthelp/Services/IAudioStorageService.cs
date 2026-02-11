namespace SilentHelp.Services
{
    public interface IAudioStorageService
    {
        Task<string> SaveAudioAsync(IFormFile file, Guid userId);
    }
}
