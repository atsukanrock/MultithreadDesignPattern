namespace ImageProcessor.MultithreadWorker
{
    internal class OriginalImageInfo
    {
        public string FileName { get; set; }

        public string ContentType { get; set; }

        public byte[] FileBytes { get; set; }
    }
}