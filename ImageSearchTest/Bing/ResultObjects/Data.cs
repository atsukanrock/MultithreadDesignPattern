using System.Collections.Generic;

namespace ImageSearchTest.Bing.ResultObjects
{
    public class Data
    {
        public List<Result> results { get; set; }
        public string __next { get; set; }
    }
}