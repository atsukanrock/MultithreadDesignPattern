using System;
using ImageSearchTest.Bing.ResultObjects;

namespace ImageProcessor.Admin.Models
{
    internal class ImageSearchedEventArgs : EventArgs
    {
        private readonly string _keyword;
        private readonly ImageSearchObject _imageSearchResult;

        public ImageSearchedEventArgs(string keyword, ImageSearchObject imageSearchResult)
        {
            _keyword = keyword;
            _imageSearchResult = imageSearchResult;
        }

        public string Keyword
        {
            get { return _keyword; }
        }

        public ImageSearchObject ImageSearchResult
        {
            get { return _imageSearchResult; }
        }
    }
}