using Microsoft.AspNetCore.Mvc;

namespace Messenger.API.InnerDto
{
    public class UploadAudioFileChunkViewModel
    {
        [FromForm(Name = "file")]
        public IFormFile file { get; set; }

        [FromForm(Name = "recordingId")]
        public string recordingId { get; set; }
        
        [FromForm(Name = "chunkIndex")]
        public int chunkIndex { get; set; }

        [FromForm(Name = "isLastChunk")]
        public bool isLastChunk { get; set; }
    }

    public class UploadFileChunkViewModel
    {
        [FromForm(Name = "file")]
        public IFormFile file { get; set; }

        [FromForm(Name = "uploadId")]
        public string uploadId { get; set; }
        
        [FromForm(Name = "chunkIndex")]
        public int chunkIndex { get; set; }

        [FromForm(Name = "totalChunks")]
        public int totalChunks { get; set; }

        [FromForm(Name = "originalFileName")]
        public string originalFileName { get; set; }
    }

//    [FromForm]
//        IFormFile file,
//[FromForm] string uploadId,
//[FromForm] int chunkIndex,
//[FromForm] int totalChunks,
//[FromForm] string originalFileName


}
