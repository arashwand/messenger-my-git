using System;
using System.Collections.Generic;

namespace Messenger.Tools;
public static class FileIconHelper
{
    /// <summary>
    /// Returns the appropriate FontAwesome icon class and file type based on file extension.
    /// </summary>
    /// <param name="extension">File extension (with or without the dot)</param>
    /// <returns>Tuple containing FontAwesome icon class name and file type in uppercase</returns>
    public static (string IconClass, string FileType) GetFontAwesomeIcon(string extension)
    {
        // Clean the extension (remove dot if present and convert to lowercase)
        if (string.IsNullOrEmpty(extension))
            return ("fa-file" ,""); // Default icon for unknown or empty extension

        // Remove the dot if it exists at the beginning
        extension = extension.TrimStart('.').ToLowerInvariant();

        // Dictionary mapping file extensions to FontAwesome icons and file types
        var extensionData = new Dictionary<string, (string IconClass, string FileType)>(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            { "pdf", ("fa-file-pdf", "PDF DOCUMENT") },
            { "doc", ("fa-file-word", "WORD DOCUMENT") },
            { "docx", ("fa-file-word", "WORD DOCUMENT") },
            { "xls", ("fa-file-excel", "EXCEL SPREADSHEET") },
            { "xlsx", ("fa-file-excel", "EXCEL SPREADSHEET") },
            { "csv", ("fa-file-csv", "CSV FILE") },
            { "ppt", ("fa-file-powerpoint", "POWERPOINT PRESENTATION") },
            { "pptx", ("fa-file-powerpoint", "POWERPOINT PRESENTATION") },
            { "txt", ("fa-file-lines", "TEXT FILE") },
            { "rtf", ("fa-file-lines", "RICH TEXT FORMAT") },
            
            // Images
            { "jpg", ("fa-file-image", "JPEG IMAGE") },
            { "jpeg", ("fa-file-image", "JPEG IMAGE") },
            { "png", ("fa-file-image", "PNG IMAGE") },
            { "gif", ("fa-file-image", "GIF IMAGE") },
            { "bmp", ("fa-file-image", "BITMAP IMAGE") },
            { "svg", ("fa-file-image", "SVG IMAGE") },
            { "webp", ("fa-file-image", "WEBP IMAGE") },
            
            // Audio
            { "mp3", ("fa-file-audio", "MP3 AUDIO") },
            { "wav", ("fa-file-audio", "WAV AUDIO") },
            { "ogg", ("fa-file-audio", "OGG AUDIO") },
            { "flac", ("fa-file-audio", "FLAC AUDIO") },
            { "aac", ("fa-file-audio", "AAC AUDIO") },
            
            // Video
            { "mp4", ("fa-file-video", "MP4 VIDEO") },
            { "avi", ("fa-file-video", "AVI VIDEO") },
            { "mov", ("fa-file-video", "MOV VIDEO") },
            { "wmv", ("fa-file-video", "WMV VIDEO") },
            { "mkv", ("fa-file-video", "MKV VIDEO") },
            { "webm", ("fa-file-video", "WEBM VIDEO") },
            
            // Archives
            { "zip", ("fa-file-zipper", "ZIP ARCHIVE") },
            { "rar", ("fa-file-zipper", "RAR ARCHIVE") },
            { "7z", ("fa-file-zipper", "7Z ARCHIVE") },
            { "tar", ("fa-file-zipper", "TAR ARCHIVE") },
            { "gz", ("fa-file-zipper", "GZIP ARCHIVE") },
            
            // Code
            { "html", ("fa-file-code", "HTML FILE") },
            { "htm", ("fa-file-code", "HTML FILE") },
            { "css", ("fa-file-code", "CSS FILE") },
            { "js", ("fa-file-code", "JAVASCRIPT FILE") },
            { "ts", ("fa-file-code", "TYPESCRIPT FILE") },
            { "json", ("fa-file-code", "JSON FILE") },
            { "xml", ("fa-file-code", "XML FILE") },
            { "cs", ("fa-file-code", "C# SOURCE FILE") },
            { "java", ("fa-file-code", "JAVA SOURCE FILE") },
            { "py", ("fa-file-code", "PYTHON SOURCE FILE") },
            { "php", ("fa-file-code", "PHP SOURCE FILE") },
            { "rb", ("fa-file-code", "RUBY SOURCE FILE") },
            { "cpp", ("fa-file-code", "C++ SOURCE FILE") },
            { "c", ("fa-file-code", "C SOURCE FILE") },
            { "h", ("fa-file-code", "HEADER FILE") },
            { "go", ("fa-file-code", "GO SOURCE FILE") },
            { "swift", ("fa-file-code", "SWIFT SOURCE FILE") },
            
            // Executables
            { "exe", ("fa-file-circle-exclamation", "EXECUTABLE FILE") },
            { "dll", ("fa-file-circle-exclamation", "DYNAMIC LINK LIBRARY") },
            { "bat", ("fa-file-circle-exclamation", "BATCH FILE") },
            { "msi", ("fa-file-circle-exclamation", "WINDOWS INSTALLER") },
            
            // Special files
            { "sql", ("fa-database", "SQL FILE") },
            { "db", ("fa-database", "DATABASE FILE") },
            { "sqlite", ("fa-database", "SQLITE DATABASE") },
            { "accdb", ("fa-database", "ACCESS DATABASE") },
            { "md", ("fa-file-lines", "MARKDOWN FILE") },
            { "log", ("fa-file-lines", "LOG FILE") },
            { "ini", ("fa-gear", "CONFIGURATION FILE") },
            { "config", ("fa-gear", "CONFIGURATION FILE") },
            { "yaml", ("fa-gear", "YAML FILE") },
            { "yml", ("fa-gear", "YAML FILE") }
        };

        // Return the specific icon and file type if the extension is found, otherwise return a generic file icon and type
        return extensionData.TryGetValue(extension, out var result)
            ? result
            : ("fa-file", "UNKNOWN FILE");
    }
}