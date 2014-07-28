using System.Web.Mvc;
using ImageProcessor.Web.Hubs;
using ImageProcessor.Web.Models.Home;

namespace ImageProcessor.Web.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View(new IndexViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(IndexViewModel viewModel)
        {
            var keyword = viewModel.Keyword;

            // Push the keyword to SignalR client on the admin WPF app.

            //if (!string.IsNullOrEmpty(viewModel.AcceptedKeywordsJson))
            //{
            //    viewModel.AcceptedKeywords.AddRange(
            //        JsonConvert.DeserializeObject<IEnumerable<string>>(viewModel.AcceptedKeywordsJson));
            //}
            //viewModel.AcceptedKeywords.Add(keyword);
            //viewModel.AcceptedKeywordsJson = JsonConvert.SerializeObject(viewModel.AcceptedKeywords);

            return View(viewModel);
        }
    }
}