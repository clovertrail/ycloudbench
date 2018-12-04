using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SignalRGameServer.Models;
using SignalRGameServer.Services.Interfaces;
using SignalRUtils;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignalRGameServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly SecurityKey signingKey;

        private readonly JwtSecurityTokenHandler jwtTokenHandler;

        private readonly SigningCredentials signingCreds;

        private readonly string issuer;

        private readonly string audience;

        private readonly SignalRServiceUtils signalRServiceUtils;

        private readonly SignalROptions signalROptions;


        public AuthService(IOptions<AuthOptions> options
            , IOptions<SignalROptions> iSignalROptions)
        {
            var authOptions = options.Value;
            signingKey = new SymmetricSecurityKey(Convert.FromBase64String(authOptions.SigningKey));
            issuer = authOptions.Issuer;
            audience = authOptions.Audience;
            jwtTokenHandler = new JwtSecurityTokenHandler();
            signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            signalROptions = iSignalROptions.Value;
            signalRServiceUtils = new SignalRServiceUtils(signalROptions.ConnectionString);
        }

        /// <summary>
        /// 用户登录，成功则返回token
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public string Login(string name, string password, out string error)
        {
            error = "";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, name),
                //new Claim(ClaimTypes.Role, role)
            };

            var claimsIdentity = new ClaimsIdentity(claims);

            var token = jwtTokenHandler.CreateJwtSecurityToken(
                issuer: issuer,
                audience: audience,
                subject: claimsIdentity,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: signingCreds
            );

            return jwtTokenHandler.WriteToken(token);
        }

        public TokenValidationParameters GetTokenValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = signingCreds.Key
            };
        }

        public SignalRAccessInfo GetSignalRServiceAccessInfo(string userId, TimeSpan lifetime)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }
            IEnumerable<Claim> claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            string url = signalRServiceUtils.GetClientUrl(signalROptions.HubName);
            DateTimeOffset expire = DateTimeOffset.UtcNow.Add(lifetime);
            string token = signalRServiceUtils.GenerateAccessToken(url, claims, expire.UtcDateTime);
            return new SignalRAccessInfo
            {
                Url = url,
                Token = token,
                Expire = expire.ToUnixTimeSeconds()
            };
        }
    }
}
