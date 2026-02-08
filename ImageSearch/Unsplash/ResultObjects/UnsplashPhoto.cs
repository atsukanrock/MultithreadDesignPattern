namespace ImageSearchTest.Unsplash.ResultObjects
{
    public class UnsplashPhoto
    {
        public string? id { get; set; }
        public string? description { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public UnsplashUrls? urls { get; set; }
    }
}
