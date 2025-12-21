using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    /// <summary>
    /// یک مدل برای شناسایی یک چت خاص (گروه، کانال یا خصوصی).
    /// </summary>
    public class ChatIdentifier
    {
        /// <summary>
        /// شناسه چت، گروه یا کانال.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// نوع چت: "group", "channel", "private"
        /// </summary>
        public string ChatType { get; set; } = string.Empty;
    }
}
