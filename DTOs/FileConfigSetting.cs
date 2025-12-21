using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class FileConfigSetting
    {
        /// <summary>
        /// مسیر اصلی ذخیره سازی فایلهای بارگذاری شده در روت اصلی پروژه
        /// همچنین اگه تصویر بود با همین اسم یک پوشه در wwwroot  ایجاد میشه
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// پوشه جهت بارگذاری تصاویر کوچک شده درون wwwroot
        /// </summary>
        public string BasePathThumbnails { get; set; }

        public string[] AllowedExtensions { get; set; }
        public string[] AllowedImageExtentions { get; set; }
        public string[] AllowedAudioExtentions { get; set; }
        public int MaxFileSizeMB { get; set; }
    }
}
