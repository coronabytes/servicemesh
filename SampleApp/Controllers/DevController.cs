using Core.ServiceMesh.Abstractions;
using Microsoft.AspNetCore.Mvc;
using SampleInterfaces;

namespace SampleApp.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController(IServiceMesh mesh, ISomeService someService) : ControllerBase
{
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromQuery] string message)
    {
        await mesh.PublishAsync(new SomeCommand(message));
        return Ok();
    }

    [HttpPost("publish-other")]
    public async Task<IActionResult> PublishOther([FromQuery] string message, [FromQuery] int count = 1)
    {
        for (int i = 0; i < count; i++)
            await mesh.PublishAsync(new SomeOtherCommand(message));
        return Ok();
    }

    [HttpPost("send")]
    public async Task<IActionResult> Broadcast([FromQuery] string message)
    {
        await mesh.SendAsync(new SomeCommand(message));
        return Ok();
    }

    [HttpPost("request")]
    public async Task<ActionResult<string>> GetSomeString([FromQuery] string message = "test", [FromQuery] int a = 3)
    {
        var res = await someService.GetSomeString(a, message);
        return res;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateObject()
    {
        await someService.CreateSomeObject();
        return Ok();
    }

    [HttpPost("add-ints")]
    public async Task<ActionResult<int>> CreateIntObject([FromQuery] int a = 3, [FromQuery] int b = 5)
    {
        return await someService.GenericAdd(a, b);
    }

    [HttpPost("add-doubles")]
    public async Task<ActionResult<double>> CreateDoubleObject([FromQuery] double a = 3.1, [FromQuery] double b = 5.1)
    {
        return await someService.GenericAdd(a, b);
    }
}