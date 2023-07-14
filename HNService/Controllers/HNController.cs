using HNService.Models;
using HNService.Services;
using Microsoft.AspNetCore.Mvc;

namespace HNService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HNController : ControllerBase
    {
        private readonly ILogger<HNController> _logger;
        private readonly IHNDataService _hnDataService;

        public HNController(ILogger<HNController> logger,
            IHNDataService hnDataService)
        {
            _logger = logger;
            _hnDataService = hnDataService;
        }

        [HttpGet("{numberOfStories}")]
        public async Task<IActionResult> BestStories(int numberOfStories)
        {
            if (numberOfStories <= 0)
            {
                _logger.LogInformation(string.Format("Fewer stories requested : {0}", numberOfStories));
                return StatusCode(400, "Fewer stories requested");
            }
            else if (numberOfStories > _hnDataService.Count)
            {
                _logger.LogInformation(string.Format("More stories requested than available : {0}", numberOfStories));
                return StatusCode(400, "More stories requested than available");
            }

            HNData[]? data;
            try
            {
                data = await Task.FromResult(_hnDataService.GetBestStories(numberOfStories).Result.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format("Failed to get data : {0}", ex.Message));
                return StatusCode(400, "Failed to get data");
            }
            return Ok(data);
        }
    }
}
