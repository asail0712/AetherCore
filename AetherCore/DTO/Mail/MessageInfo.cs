using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.DTO.Mail
{
    public class MessageInfo
    {
        public string ToEmail   { get; set; } = string.Empty;
        public string Subject   { get; set; } = string.Empty;
        public string TextBody  { get; set; } = string.Empty;
    }
}
