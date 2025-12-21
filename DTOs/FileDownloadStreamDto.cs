using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class FileDownloadStreamDto
    {
        public Stream ContentStream { get; }
        public string ContentType { get; }
        public string FileName { get; }

        public FileDownloadStreamDto(Stream contentStream, string contentType, string fileName)
        {
            ContentStream = contentStream;
            ContentType = contentType;
            FileName = fileName;
        }
    }
}
