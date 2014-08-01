using System.Web.Http;
using ImageProcessor.Web.Hubs;
using Microsoft.AspNet.SignalR;

namespace ImageProcessor.Web.Controllers
{
    public class KeywordsController : ApiController
    {
        public void Post([FromBody]string keyword)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<KeywordHub>();
            context.Clients.All.addPostedKeyword(keyword);
        }
    }
}