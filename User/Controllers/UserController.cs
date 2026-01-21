using Elitech.Models;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Elitech.Controllers
{
    [ApiController]
    [Route("api/elitech")]
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly ElitechDeviceAssignmentService _assign;
        public UserController(ElitechDeviceAssignmentService assign)
        {
            _assign = assign;
        }
        public IActionResult Index() => View();
       

    }
}