using Microsoft.AspNetCore.Mvc;

namespace ScalingWrites.Core.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Post()
    {

    }
}
