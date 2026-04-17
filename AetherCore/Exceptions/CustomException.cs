using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Exceptions
{
    // 自訂基底例外類別，繼承自 System.Exception
    public class CustomException : Exception
    {
        public CustomException(string message)
            : base(message)
        { }

        public CustomException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
