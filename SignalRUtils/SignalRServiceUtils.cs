using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace SignalRUtils
{
    public class SignalRServiceUtils
    {
        private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();

        public string Endpoint { get; }

        public string AccessKey { get; }

        public string Version { get; }

        public int? Port { get; }

        public SignalRServiceUtils(string connectionString)
        {
            (Endpoint, AccessKey, Version, Port) = ParseConnectionString(connectionString);
        }

        public string GenerateAccessToken(string audience, string userId, TimeSpan? lifetime = null)
        {
            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            return GenerateAccessToken(audience, claims, DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)));
        }

        public string GenerateAccessToken(string audience, IEnumerable<Claim> claims, DateTime expire)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccessKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: claims == null ? null : new ClaimsIdentity(claims),
                expires: expire,
                signingCredentials: credentials);
            return JwtTokenHandler.WriteToken(token);
        }

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const string VersionProperty = "version";
        private const string PortProperty = "port";
        // For SDK 1.x, only support Azure SignalR Service 1.x
        private const string SupportedVersion = "1";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";
        private const string InvalidVersionValueFormat = "Version {0} is not supported.";

        private static readonly string InvalidPortValue = $"Invalid value for {PortProperty} property.";

        internal static (string, string, string version, int? port) ParseConnectionString(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length > 1)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    var kvp = property.Split(KeyValueSeparator, 2);
                    if (kvp.Length != 2) continue;

                    var key = kvp[0].Trim();
                    if (dict.ContainsKey(key))
                    {
                        throw new ArgumentException($"Duplicate properties found in connection string: {key}.");
                    }

                    dict.Add(key, kvp[1].Trim());
                }

                string version = null;
                if (dict.TryGetValue(VersionProperty, out var v))
                {
                    if (Regex.IsMatch(v, ValidVersionRegex))
                    {
                        version = v;
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(InvalidVersionValueFormat, v), nameof(connectionString));
                    }
                }

                int? port = null;
                if (dict.TryGetValue(PortProperty, out var s))
                {
                    if (int.TryParse(s, out var p) &&
                        p > 0 && p <= 0xFFFF)
                    {
                        port = p;
                    }
                    else
                    {
                        throw new ArgumentException(InvalidPortValue, nameof(connectionString));
                    }
                }

                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty], version, port);
                }
            }

            throw new ArgumentException($"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.");
        }

        public string GetSendToUserUrl(string hubName, string userId)
        {
            return $"{GetBaseUrl(hubName)}/users/{userId}";
        }

        public string GetSendToGroupUrl(string hubName, string groupId)
        {
            return $"{GetBaseUrl(hubName)}/groups/{groupId}";
        }

        public string GetBroadcastUrl(string hubName)
        {
            return $"{GetBaseUrl(hubName)}";
        }

        public string GetGroupActionUrl(string hubName, string userId, string groupId)
        {
            return $"{GetBaseUrl(hubName)}/groups/{groupId}/users/{userId}";
        }

        public string GetBaseUrl(string hubName)
        {
            return $"{Endpoint}/api/v1/hubs/{hubName.ToLower()}";
        }
        public Uri GetUrl(string baseUrl)
        {
            return new UriBuilder(baseUrl).Uri;
        }

        public string GetClientUrl(string hubName)
        {
            return Port.HasValue ?
                $"{Endpoint}:{Port}/client/?hub={hubName}" :
                $"{Endpoint}/client/?hub={hubName}";
        }

        public HttpRequestMessage BuildRequest(string url, PayloadMessage payload, HttpMethod httpMethod = null)
        {
            if (httpMethod == null)
            {
                httpMethod = HttpMethod.Post;
            }
            var request = new HttpRequestMessage(httpMethod, GetUrl(url));

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", GenerateAccessToken(url, ""));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (payload != null)
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            }
            return request;
        }
    }
}
