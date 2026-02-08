namespace ImageSearchTest.Unsplash.ResultObjects
{
    public class UnsplashSearchResult
    {
        public int total { get; set; }
        public int total_pages { get; set; }
        public List<UnsplashPhoto>? results { get; set; }
    }
}
