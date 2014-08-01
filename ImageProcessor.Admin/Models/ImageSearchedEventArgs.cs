using System;
using ImageProcessor.Storage.Queue.Messages;

namespace ImageProcessor.Admin.Models
{
    internal class ImageSearchedEventArgs : EventArgs
    {
        private readonly ProcessingRequestMessage _processingRequestMessage;

        public ImageSearchedEventArgs(ProcessingRequestMessage processingRequestMessage)
        {
            _processingRequestMessage = processingRequestMessage;
        }

        public ProcessingRequestMessage ProcessingRequestMessage
        {
            get { return _processingRequestMessage; }
        }
    }
}