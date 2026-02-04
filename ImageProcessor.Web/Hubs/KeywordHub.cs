using Microsoft.AspNetCore.SignalR;

namespace ImageProcessor.Web.Hubs;

public class KeywordHub : Hub
{
    public async Task Post(string keyword)
    {
        await Clients.All.SendAsync("addPostedKeyword", keyword);
    }
}