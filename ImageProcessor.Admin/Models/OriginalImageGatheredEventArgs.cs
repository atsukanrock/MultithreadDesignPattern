using System;
using ImageProcessor.Storage.Queue.Messages;

namespace ImageProcessor.Admin.Models
{
    internal class OriginalImageGatheredEventArgs : EventArgs
    {
        private readonly ProcessingRequestMessage _processingRequestMessage;

        public OriginalImageGatheredEventArgs(ProcessingRequestMessage processingRequestMessage)
        {
            _processingRequestMessage = processingRequestMessage;
        }

        public ProcessingRequestMessage ProcessingRequestMessage
        {
            get { return _processingRequestMessage; }
        }
    }
}