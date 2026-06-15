using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

[ApiController]
[Authorize]
[Route("v1/[controller]")]
public abstract class BaseApiController : ControllerBase { }
