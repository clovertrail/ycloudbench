using Microsoft.AspNetCore.Mvc;
using SignalRGameServer.Services.Interfaces;
using SignalRUtils;
using System;

namespace SignalRGameServer.Controllers
{
    [Route("auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        readonly IAuthService authService;

        public AuthController(IAuthService authService)
        {
            this.authService = authService;
        }

        [HttpGet("login")]
        public IActionResult Login([FromQuery] string username, [FromQuery] string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Ok(new
                {
                    token = "",
                    url = "",
                    expire = 0,
                    error = "Username and Password is required."
                });
            }

            string token = authService.Login(username, password, out string error);
            if (string.IsNullOrEmpty(token))
            {
                return Ok(new
                {
                    token = "",
                    url = "",
                    expire = 0,
                    error
                });
            }

            SignalRAccessInfo accessInfo = authService.GetSignalRServiceAccessInfo(username, TimeSpan.FromDays(100));
            if (accessInfo == null)
            {
                return Ok(new
                {
                    token = "",
                    url = "",
                    expire = 0,
                    error = "cannot get accessInfo"
                });
            }
            return Ok(new
            {
                token = accessInfo.Token,
                url = accessInfo.Url,
                expire = accessInfo.Expire,
                tag = "v1",
                interval = 1000,
                error = ""
            });
        }
    }
}