using Microsoft.AspNet.SignalR;

namespace ImageProcessor.Web.Hubs
{
    public class KeywordHub : Hub
    {
        public void Post(string keyword)
        {
            Clients.All.broadcastKeyword(keyword);
        }
    }
}