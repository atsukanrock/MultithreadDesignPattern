using Microsoft.AspNetCore.Mvc;
using ImageProcessor.Web.Models.Home;

namespace ImageProcessor.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(new IndexViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(IndexViewModel viewModel)
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}