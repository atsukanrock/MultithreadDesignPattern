using System;

namespace ImageProcessor.Admin.Models
{
    class ImageProcessedEventArgs : EventArgs
    {
        private readonly string _resultFileName;

        public ImageProcessedEventArgs(string resultFileName)
        {
            _resultFileName = resultFileName;
        }

        public string ResultFileName
        {
            get { return _resultFileName; }
        }
    }
}