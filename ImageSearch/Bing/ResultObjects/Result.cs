namespace ImageSearchTest.Bing.ResultObjects
{
    public class Result
    {
        public Metadata? __metadata { get; set; }
        public string? ID { get; set; }
        public string? Title { get; set; }
        public string? MediaUrl { get; set; }
        public string? SourceUrl { get; set; }
        public string? DisplayUrl { get; set; }
        public string? Width { get; set; }
        public string? Height { get; set; }
        public string? FileSize { get; set; }
        public string? ContentType { get; set; }
        public Thumbnail? Thumbnail { get; set; }
    }
}