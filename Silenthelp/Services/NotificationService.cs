using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SilentHelp.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IConfiguration config, ILogger<NotificationService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Send Push Notification via Firebase
        public async Task SendPushNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
        {
            try
            {
                _logger.LogInformation($"[v0] Sending push notification to device token: {deviceToken}");

                // Firebase implementation would go here
                // For now, just log it
                _logger.LogInformation($"[v0] Push Notification: {title} - {body}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[v0] Error sending push notification: {ex.Message}");
            }
        }

        // Send SMS via Twilio
        public async Task SendSMSAsync(string phoneNumber, string message)
        {
            try
            {
                var accountSid = _config["Twilio:AccountSid"];
                var authToken = _config["Twilio:AuthToken"];
                var fromNumber = _config["Twilio:FromNumber"];

                // Skip SMS if Twilio is not configured
                if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
                {
                    _logger.LogWarning($"[v0] Twilio not configured, skipping SMS to {phoneNumber}");
                    return;
                }

                TwilioClient.Init(accountSid, authToken);

                var sms = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(fromNumber),
                    to: new PhoneNumber(phoneNumber)
                );

                _logger.LogInformation($"[v0] SMS sent with SID: {sms.Sid}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[v0] Error sending SMS: {ex.Message}");
            }
        }
    }
}
