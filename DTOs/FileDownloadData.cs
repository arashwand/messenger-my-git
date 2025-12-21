using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public record FileDownloadData(byte[] Content, string ContentType, string FileName);
}
