using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ImageProcessor.Web.Hubs;

namespace ImageProcessor.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KeywordsController : ControllerBase
{
    private readonly IHubContext<KeywordHub> _hubContext;

    public KeywordsController(IHubContext<KeywordHub> hubContext)
    {
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task Post([FromBody] string keyword)
    {
        await _hubContext.Clients.All.SendAsync("addPostedKeyword", keyword);
    }
}