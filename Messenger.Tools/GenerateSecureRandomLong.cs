using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public static class GenerateSecureRandomLong
    {
        public static long Generate()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[8];
                rng.GetBytes(bytes);
                long random = BitConverter.ToInt64(bytes, 0);
                // Ensure the number is positive and fits within a safe range for JavaScript
                return Math.Abs(random % 9007199254740991L);
            }
        }
    }
}
