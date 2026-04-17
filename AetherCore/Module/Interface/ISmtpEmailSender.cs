using AetherCore.DTO.Mail;

namespace AetherCore.Module.Interface
{
    public interface ISmtpEmailSender
    {
        Task<bool> SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken ct = default);
        Task<bool> SendAsync(MessageInfo info, SenderGroup senderGroup, CancellationToken ct = default);
    }
}
