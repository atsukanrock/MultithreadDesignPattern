using System.ComponentModel.DataAnnotations;

namespace ImageProcessor.Web.Models.Home;

public class IndexViewModel
{
    private readonly List<string> _acceptedKeywords = new();

    [Required]
    [StringLength(255)]
    public string Keyword { get; set; } = string.Empty;

    //public List<string> AcceptedKeywords
    //{
    //    get { return _acceptedKeywords; }
    //}

    //public string AcceptedKeywordsJson { get; set; }
}