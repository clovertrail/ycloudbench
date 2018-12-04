using Microsoft.IdentityModel.Tokens;
using SignalRUtils;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRGameServer.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// 用户登录，成功则返回token
        /// </summary>
        /// <param name="name">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="error"></param>
        /// <returns></returns>
        string Login(string name, string password, out string error);

        TokenValidationParameters GetTokenValidationParameters();

        /// <summary>
        /// 用户访问Signal的token
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="hubname"></param>
        /// <param name="lifetime"></param>
        /// <returns></returns>
        SignalRAccessInfo GetSignalRServiceAccessInfo(string userId, TimeSpan lifetime);
    }
}
