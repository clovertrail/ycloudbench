﻿using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceTest
{
    class Tester
    {
        private HubConnection connection;

        protected NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool IsConnected { get; protected set; } = false;

        public bool IsConnecting { get; protected set; } = false;

        private readonly string host;

        private AccessInfo accessInfo;

        private readonly string userId;

        /// <summary>
        /// 每多少个消息统计一次平均值
        /// </summary>
        public int AvgCount { set; get; } = 100;

        private DateTimeOffset lastConnectTime = DateTimeOffset.MinValue;

        public Tester(string host, string userId = null)
        {
            this.host = host;
            if (string.IsNullOrWhiteSpace(userId))
            {
                this.userId = Guid.NewGuid().ToString();
            }
            else
            {
                this.userId = userId;
            }
            Connect().Wait();
        }

        public async Task Connect()
        {
            if (IsConnected || IsConnecting)
            {
                return;
            }
            IsConnecting = true;
            TimeSpan sleep = lastConnectTime.AddSeconds(3) - DateTimeOffset.UtcNow;
            if (sleep.TotalMilliseconds > 0)
            {
                Thread.Sleep(sleep);
            }

            lastConnectTime = DateTimeOffset.UtcNow;

            (string url, string accessToken) = GetAuth();
            //logger.Info($"response: url: {url}, accessToken: {accessToken}");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(accessToken))
            {
                return;
            }

            connection = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                    options.CloseTimeout = TimeSpan.FromSeconds(15);
                    options.Transports = HttpTransportType.WebSockets; // Note: skip negotiation requires to specify transport type explicitly.
                    options.SkipNegotiation = true;
                    logger.Info("options");
                })
                //.AddMessagePackProtocol()
                .Build();

            connection.Closed += ConnectionClosed;

            connection.On("Connected", OnConnected);

            long totalDelay = 0;
            long min = long.MaxValue;
            long max = 0;
            int responseCount = 0;
            object locker = new object();
            connection.On<MessageBody>("PerformanceTest", message =>
            {
                long delay = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.CreatedTime.ToUnixTimeMilliseconds();
                logger.Debug($"PerformanceTest Result: userId:{userId},delay={delay} ms");
                lock (locker)
                {
                    totalDelay += delay;
                    if (delay > max)
                    {
                        max = delay;
                    }
                    if (delay < min)
                    {
                        min = delay;
                    }
                    responseCount++;
                    if (responseCount == AvgCount)
                    {
                        logger.Info($"User {userId} latest {AvgCount} perf result min: {min} mx;max: {max} ms;avg: {totalDelay / AvgCount} ms");
                        responseCount = 0;
                        min = long.MaxValue;
                        max = 0;
                        totalDelay = 0;
                    }
                }
            });

            lastConnectTime = DateTimeOffset.UtcNow;
            try
            {
                await connection.StartAsync();
            }
            catch (Exception e)
            {
                logger.Error($"Fail to connect: {e.Message}");
            }
        }

        private Task ConnectionClosed(Exception ex)
        {
            IsConnected = false;
            IsConnecting = false;
            logger.Error(ex, $"user {userId} ConnectionClosed");
            return connection.DisposeAsync();
        }

        private void OnConnected()
        {
            logger.Info($"user {userId} Connected,cost {(DateTimeOffset.UtcNow - lastConnectTime).TotalMilliseconds} ms");
            IsConnected = true;
            IsConnecting = false;
        }

        private (string, string) GetAuth()
        {
            if (accessInfo == null || accessInfo.TokenExpire < DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds())
            {
                string result = new HttpClient().GetStringAsync($"{host}/auth/login?username={userId}&password=admin").GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(result))
                {
                    logger.Error($"用户 {userId}登录失败");
                    return (null, null);
                }

                try
                {
                    accessInfo = JsonConvert.DeserializeObject<AccessInfo>(result);
                    if (!string.IsNullOrEmpty(accessInfo.Error))
                    {
                        logger.Error($"用户 {userId} 登录失败," + accessInfo.Error);
                        accessInfo = null;
                        return (null, null);
                    }
                    return (accessInfo.SignalRUrl, accessInfo.SignalRToken);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"用户 {userId} 登录失败");
                    return (null, null);
                }
            }
            return (accessInfo.SignalRUrl, accessInfo.SignalRToken);
        }

        public void RunTest(byte[] content)
        {
            if (IsConnected)
            {
                // Here ignore the result of send
                _ = connection.SendAsync("PerformanceTest", new MessageBody
                {
                    CreatedTime = DateTimeOffset.UtcNow,
                    Contnet = content
                });
            }
        }
    }
}
