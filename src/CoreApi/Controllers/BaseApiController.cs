using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

[ApiController]
[Route("v1/[controller]")]
public abstract class BaseApiController : ControllerBase { }
