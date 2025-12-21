using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class SharedFileDto
    {
        public long MessageFileId { get; set; }
        public string FileName { get; set; }
        public string OriginalFileName { get; set; }
        public string FilePath { get; set; }
        public string FileThumbPath { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; } // "media", "document"
        public DateTime SentAt { get; set; }
    }

    public class SharedLinkDto
    {
        public long MessageId { get; set; }
        public string LinkUrl { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class SharedContentDto
    {
        public List<SharedFileDto> MediaFiles { get; set; } = new List<SharedFileDto>();
        public List<SharedFileDto> DocumentFiles { get; set; } = new List<SharedFileDto>();
        public List<SharedLinkDto> Links { get; set; } = new List<SharedLinkDto>();

        public string ActiveTab { get; set; }
        public string BaseUrl { get; set; }
    }

    public class CountSharedContentDto
    {
        public int MediaFilesCount { get; set; }
        public int DocumentFilesCount { get; set; }
        public int LinkFilesCount { get; set; }
    }
}
