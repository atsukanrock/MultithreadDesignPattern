using System;
using ImageSearchTest.Unsplash.ResultObjects;

namespace ImageProcessor.Admin.Models
{
    internal class ImageSearchedEventArgs : EventArgs
    {
        private readonly string _keyword;
        private readonly UnsplashSearchResult _imageSearchResult;

        public ImageSearchedEventArgs(string keyword, UnsplashSearchResult imageSearchResult)
        {
            _keyword = keyword;
            _imageSearchResult = imageSearchResult;
        }

        public string Keyword
        {
            get { return _keyword; }
        }

        public UnsplashSearchResult ImageSearchResult
        {
            get { return _imageSearchResult; }
        }
    }
}
