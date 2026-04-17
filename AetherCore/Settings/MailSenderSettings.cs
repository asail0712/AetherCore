using AetherCore.Utility.Attributes;

namespace AetherCore.Settings
{
    [AppSettings]
    public class MailSenderSettings
    {
        public string FromName { get; set; }    = string.Empty;
        public string FromMail { get; set; }    = string.Empty;
        public string Host { get; set; }        = string.Empty;
        public int Port { get; set; }           = 0;
        public string UserName { get; set; }    = string.Empty;
        public string Password { get; set; }    = string.Empty;
    }
}
