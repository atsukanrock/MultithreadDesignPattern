using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImageProcessor.Storage.Queue.Messages
{
    public class ProcessingRequestMessage
    {
        private readonly string _keyword;
        private readonly IReadOnlyCollection<string> _fileNames;

        public ProcessingRequestMessage(string keyword, IEnumerable<string> fileNames)
        {
            _keyword = keyword;
            _fileNames = new ReadOnlyCollection<string>(fileNames.ToArray());
        }

        public string Keyword
        {
            get { return _keyword; }
        }

        public IReadOnlyCollection<string> FileNames
        {
            get { return _fileNames; }
        }
    }
}