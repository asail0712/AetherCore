using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.DTO.Mail
{
    public class SenderGroup
    {
        public string FromName { get; set; }    = string.Empty;
        public string FromMail { get; set; }    = string.Empty;
        public string Host { get; set; }        = string.Empty;
        public int Port { get; set; }           = 0;
        public string UserName { get; set; }    = string.Empty;
        public string Password { get; set; }    = string.Empty;
    }
}
