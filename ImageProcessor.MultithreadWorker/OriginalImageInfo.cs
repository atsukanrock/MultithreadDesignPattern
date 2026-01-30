namespace ImageProcessor.MultithreadWorker;

internal class OriginalImageInfo
{
    public OriginalImageInfo(string fileName, string contentType, byte[] fileBytes)
    {
        FileName = fileName;
        ContentType = contentType;
        FileBytes = fileBytes;
    }

    public string FileName { get; }

    public string ContentType { get; }

    public byte[] FileBytes { get; }
}