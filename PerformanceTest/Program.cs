using CommandLine;
using Newtonsoft.Json;
using SignalRUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceTest
{
    class Program
    {
        //static readonly string host = "https://golf-asrs.azurewebsites.net/";
        //static readonly string host = "http://localhost:58664/";

        public class Options
        {
            [Option('t', "ThreadCount", Required = false, Default = 1, HelpText = "并发线程数")]
            public int ThreadCount { set; get; } = 1;

            [Option('d', "DelayMilliseconds", Required = false, Default = 10, HelpText = "消息间隔时间，单位毫秒，最小10")]
            public int DelayMilliseconds { set; get; } = 10;

            [Option('m', "MessageCount", Required = false, Default = 0, HelpText = "每个线程测试的消息数，小于或等于0表示不限")]
            public int MessageCount { set; get; } = 0;

            [Option('u', "Url", Required = false, Default = "http://localhost:58664/", HelpText = "服务器地址")]
            public string Url { set; get; } = "http://localhost:58664/";

            [Option('s', "Size", Required = false, Default = 100, HelpText = "消息字节数，最大10000，最小1")]
            public int Size { set; get; } = 100;

            [Option('a', "AvgCount", Required = false, Default = 100, HelpText = "统计平均值的数目，最大1000，最小10")]
            public int AvgCount { set; get; } = 100;

            [Option('U', "UserIdPrfix", Required = false, Default = "", HelpText = "用户id前缀,最长40")]
            public string UserIdPrfix { set; get; } = "";

            [Option('c', "UseCounter", Required = false, Default = 0, HelpText = "Use counter statistic")]
            public int UseCounter { set; get; }
        }

        static void Main(string[] args)
        {
            NLog.Logger logger = NLog.LogManager.GetLogger("PerformanceTest.Program");

            //if (args.Length > 0 && args[args.Length - 1].StartsWith("&"))
            //{
            //    Array.Resize(ref args, args.Length - 1);
            //}
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(async (options) =>
                {
                    Random random = new Random();

                    if (options.ThreadCount < 1)
                    {
                        logger.Error("线程数配置错误");
                        return;
                    }
                    else if (options.Size > 10000 || options.Size < 1)
                    {
                        logger.Error("消息字节数配置错误");
                        return;
                    }
                    else if (options.DelayMilliseconds < 10)
                    {
                        logger.Error("消息间隔不能低于10毫秒");
                        return;
                    }
                    else if (string.IsNullOrWhiteSpace(options.Url) || !options.Url.StartsWith("http"))
                    {
                        logger.Error("服务器地址错误");
                        return;
                    }
                    else if (options.AvgCount < 10 || options.AvgCount > 1000)
                    {
                        logger.Error("AvgCount配置错误");
                        return;
                    }
                    else if (options.UserIdPrfix.Length > 40)
                    {
                        logger.Error("UserIdPrfix配置错误");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(options.UserIdPrfix))
                    {
                        options.UserIdPrfix = "user-" + random.Next(1000, 9999);
                    }

                    ConcurrentBag<Tester> testers = new ConcurrentBag<Tester>();

                    Counter c = null;
                    if (options.UseCounter == 1)
                    {
                        c = new Counter();
                    }
                    else
                    {
                        logger.Info("Counter is not set");
                    }

                    await Task.Run(() =>
                    {
                        logger.Info("start connections");
                        var sw = new Stopwatch();
                        sw.Start();
                        for (int i = 1; i <= options.ThreadCount; i++)
                        {
                            var tester = new Tester(options.Url, options.UserIdPrfix + "_" + i, c)
                            {
                                AvgCount = options.AvgCount
                            };
                            int retry = 10;
                            while (retry > 0)
                            {
                                retry--;
                                if (tester.IsConnected)
                                {
                                    testers.Add(tester);
                                    logger.Info($"start {i} instance");
                                    break;
                                }
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }
                        sw.Stop();
                        logger.Info($"Build {options.ThreadCount} connections takes {sw.ElapsedMilliseconds} ms");
                        logger.Info($"Finish startup with {testers.Count} instances");
                    });
                    c?.StartPrint();
                    await Task.Run(async () =>
                    {
                        logger.Info("start sending");
                        byte[] content = new byte[options.Size];
                        while (true)
                        {
                            DateTimeOffset start = DateTimeOffset.UtcNow;
                            foreach (Tester tester in testers)
                            {
                                if (!tester.IsConnected)
                                {
                                    try
                                    {
                                        await tester.Connect();
                                    }
                                    catch (Exception e)
                                    {
                                        logger.Error($"Fail to connect for {e}");
                                    }
                                }
                                else
                                {
                                    random.NextBytes(content);
                                    tester.RunTest(content);
                                }
                            }
                            TimeSpan delay = TimeSpan.FromMilliseconds(options.DelayMilliseconds) - (DateTimeOffset.UtcNow - start);
                            if (delay.TotalMilliseconds > 0)
                            {
                                Thread.Sleep(delay);
                                //logger.Info($"delay {delay}");
                            }
                        }
                    });
                    c?.Dispose();
                }).WithNotParsed<Options>(error =>
                        {
                            logger.Error("参数错误");
                        });
            Thread.Sleep(int.MaxValue);
        }
    }
}
