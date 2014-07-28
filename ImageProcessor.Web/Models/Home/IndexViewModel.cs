using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ImageProcessor.Web.Models.Home
{
    public class IndexViewModel
    {
        private readonly List<string> _acceptedKeywords = new List<string>();

        [Required]
        [StringLength(255)]
        public string Keyword { get; set; }

        //public List<string> AcceptedKeywords
        //{
        //    get { return _acceptedKeywords; }
        //}

        //public string AcceptedKeywordsJson { get; set; }
    }
}