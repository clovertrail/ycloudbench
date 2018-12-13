using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalRUtils;

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

        private long _timestampWhenConnectionClosed;

        /// <summary>
        /// 每多少个消息统计一次平均值
        /// </summary>
        public int AvgCount { set; get; } = 100;

        private DateTimeOffset lastConnectTime = DateTimeOffset.MinValue;

        private object locker = new object();

        private Counter Counter { set; get; }

        private DelayCounter DelayCounter { set; get; }

        public Tester(string host, string userId, Counter c, DelayCounter dc)
        {
            this.host = host;
            Counter = c;
            DelayCounter = dc;
            if (string.IsNullOrWhiteSpace(userId))
            {
                this.userId = Guid.NewGuid().ToString();
            }
            else
            {
                this.userId = userId;
            }
            Task.Run(async () =>
            {
                await Connect();
            });
        }

        private void RandomDelay()
        {
            var random = new Random();
            var s = random.Next(1000);
            var sleep = lastConnectTime.AddMilliseconds(s) - DateTimeOffset.UtcNow;
            if (sleep.TotalMilliseconds > 0)
            {
                Thread.Sleep(sleep);
            }
        }

        public async Task Connect()
        {
            if (IsConnected || IsConnecting)
            {
                return;
            }
            IsConnecting = true;
            /*
            TimeSpan sleep = lastConnectTime.AddSeconds(3) - DateTimeOffset.UtcNow;
            if (sleep.TotalMilliseconds > 0)
            {
                Thread.Sleep(sleep);
            }
            */
            RandomDelay();
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
                .Build();

            connection.Closed += ConnectionClosed;

            connection.On("Connected", OnConnected);

            long totalDelay = 0;
            long min = long.MaxValue;
            long max = 0;
            int responseCount = 0;

            connection.On<MessageBody>("PerformanceTest", message =>
            {
                long delay = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.CreatedTime.ToUnixTimeMilliseconds();
                //logger.Debug($"PerformanceTest Result: userId:{userId},delay={delay} ms");
                if (Counter != null)
                {
                    Counter.Latency(delay);
                }
                else
                {
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

        public async Task Reconnect()
        {
            if (IsConnected || IsConnecting)
            {
                return;
            }
            IsConnecting = true;
            lastConnectTime = DateTimeOffset.UtcNow;
            try
            {
                await connection.StartAsync();
            }
            catch (Exception e)
            {
                logger.Error($"Fail to reconnect: {e.Message}");
            }
        }

        private Task ConnectionClosed(Exception ex)
        {
            IsConnected = false;
            IsConnecting = false;
            logger.Error(ex, $"user {userId} ConnectionClosed");
            Counter.ConnectionFail();
            _timestampWhenConnectionClosed = Utils.Timestamp();
            return connection.DisposeAsync();
        }

        private void OnConnected()
        {
            logger.Info($"user {userId} Connected,cost {(DateTimeOffset.UtcNow - lastConnectTime).TotalMilliseconds} ms");
            IsConnected = true;
            IsConnecting = false;
            // record the reconnection cost
            if (_timestampWhenConnectionClosed != 0)
            {
                DelayCounter.Add(Utils.Timestamp() - _timestampWhenConnectionClosed);
                _timestampWhenConnectionClosed = 0;
            }
            Counter.ConnectionSuccess();
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
