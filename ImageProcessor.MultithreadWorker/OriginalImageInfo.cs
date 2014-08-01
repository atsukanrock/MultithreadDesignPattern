namespace ImageProcessor.MultithreadWorker
{
    internal class OriginalImageInfo
    {
        private readonly string _fileName;
        private readonly string _contentType;
        private readonly byte[] _fileBytes;

        public OriginalImageInfo(string fileName, string contentType, byte[] fileBytes)
        {
            _fileName = fileName;
            _contentType = contentType;
            _fileBytes = fileBytes;
        }

        public string FileName
        {
            get { return _fileName; }
        }

        public string ContentType
        {
            get { return _contentType; }
        }

        public byte[] FileBytes
        {
            get { return _fileBytes; }
        }
    }
}