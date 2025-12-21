using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public static class FileNameShorter
    {
        public static string ShortenFileName(string fileName, int maxLength = 20)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            string extension = Path.GetExtension(fileName); // پسوند فایل، مثل ".jpg"
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            int allowedNameLength = maxLength - extension.Length;

            if (allowedNameLength <= 0)
                return fileName; // اگر پسوند از maxLength بیشتر بود، برمی‌گردونه همون فایل اصلی رو

            if (nameWithoutExtension.Length > allowedNameLength)
                nameWithoutExtension = nameWithoutExtension.Substring(0, allowedNameLength);

            return nameWithoutExtension + extension;
        }
    }
}
