using System.Collections.ObjectModel;

namespace ImageProcessor.Storage.Queue.Messages;

public class ProcessingRequestMessage
{
    public ProcessingRequestMessage(string keyword, IEnumerable<string> fileNames)
    {
        Keyword = keyword;
        FileNames = new ReadOnlyCollection<string>(fileNames.ToArray());
    }

    public string Keyword { get; }

    public IReadOnlyCollection<string> FileNames { get; }
}