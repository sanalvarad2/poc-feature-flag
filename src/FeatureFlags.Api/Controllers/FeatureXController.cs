using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

public sealed class FeatureXController(IFeatureManager featureManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [FeatureGate("FeatureX")]
    public IActionResult Get()
    {
        return Ok(new
        {
            feature = "FeatureX",
            message = "FeatureX is enabled."
        });
    }

    [HttpGet("funcionalidad")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    
    public async Task<IActionResult> GetFuncionalidad()
    {
        var enabled = await featureManager.IsEnabledAsync("FeatureY");
        if(enabled){
            return Ok(new
        {
            feature = "FeatureY",
            message = "FeatureY is enabled."
        });
        }

        return Ok(new
        {
            feature = "FeatureX",
            message = "FeatureX is enabled."
        });
    }
}
