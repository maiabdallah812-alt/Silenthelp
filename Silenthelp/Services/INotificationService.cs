namespace SilentHelp.Services
{
    public interface INotificationService
    {
        Task SendPushNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null);
        Task SendSMSAsync(string phoneNumber, string message);
    }
}