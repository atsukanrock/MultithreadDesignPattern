using System;

namespace ImageProcessor.Admin.Models
{
    internal class ImageRetrievedEventArgs : EventArgs
    {
        private readonly string _temporaryFilePath;

        public ImageRetrievedEventArgs(string temporaryFilePath)
        {
            _temporaryFilePath = temporaryFilePath;
        }

        public string TemporaryFilePath
        {
            get { return _temporaryFilePath; }
        }
    }
}