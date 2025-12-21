using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Classes
{
    public class TimeLimitExceededException : Exception
    {
        public int AllowedMinutes { get; }

        public TimeLimitExceededException(int allowedMinutes)
            : base($"زمان مجاز برای این عملیات ({allowedMinutes} دقیقه) به پایان رسیده است.")
        {
            AllowedMinutes = allowedMinutes;
        }
    }
}
