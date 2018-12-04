using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace SignalRUtils
{
    public class Counter : IDisposable
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
        private readonly long Step = 100;    // latency unit
        private readonly long Length = 10;    // how many latency categories will be displayed
        private readonly static string OutputFile = "Counters.txt";

        private long[] _latency;
        private long _totalReceived;
        private long _totalRecvSize;
        private long _totalSent;
        private long _totalSentSize;

        private Timer _timer;
        private long _startPrint;
        private bool _hasRecord;

        private object _lock = new object();

        public Counter()
        {
            _latency = new long[Length];
        }

        public void Latency(long dur)
        {
            long index = dur / Step;
            if (index >= Length)
            {
                index = Length - 1;
            }
            Interlocked.Increment(ref _latency[index]);
            _hasRecord = true;
        }

        public void RecordSentSize(long sentSize)
        {
            Interlocked.Increment(ref _totalSent);
            Interlocked.Add(ref _totalSentSize, sentSize);
        }

        public void RecordRecvSize(long recvSize)
        {
            Interlocked.Increment(ref _totalReceived);
            Interlocked.Add(ref _totalRecvSize, recvSize);
        }

        public void StartPrint()
        {
            if (Interlocked.CompareExchange(ref _startPrint, 1, 0) == 0)
            {
                _timer = new Timer(Report, state: this, dueTime: Interval, period: Interval);
            }
        }

        private void Report(object state)
        {
            if (_hasRecord)
            {
                ((Counter)state).InternalReportDistribution();
                _hasRecord = false;
            }
        }

        private void InternalReportDistribution()
        {
            var arrCopy = new long[Length];
            _latency.CopyTo(arrCopy, 0);
            long sum = (from x in arrCopy select x).Sum();
            float le1sPercent = (sum - arrCopy[Length - 1]) * 100 / sum;
            float gt1sPercent = (arrCopy[Length - 1]) * 100 / sum;
            var le1fmt = String.Format("{0:F3}", le1sPercent);
            var gt1fmt = String.Format("{0:F3}", gt1sPercent);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(OutputFile, true))
            {
                file.WriteLine($"{DateTimeOffset.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")}: <=1s: count={sum - arrCopy[Length - 1]} percent={le1fmt}%, >1s: count={arrCopy[Length - 1]} percent={gt1fmt}% ");
            }
            //Console.WriteLine($"<=1s: count={sum - arrCopy[Length - 1]} percent={le1fmt}%, >1s: count={arrCopy[Length - 1]} percent={gt1fmt}% ");
        }

        private void InternalReport()
        {
            var dic = new ConcurrentDictionary<string, long>();
            var batchMessageDic = new ConcurrentDictionary<string, long>();
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < Length; i++)
            {
                sb.Clear();
                var label = Step + i * Step;
                if (i < Length - 1)
                {
                    sb.Append("message:lt:");
                }
                else
                {
                    sb.Append("message:ge:");
                }
                sb.Append(Convert.ToString(label));
                dic[sb.ToString()] = _latency[i];
            }
            dic["message:sent"] = Interlocked.Read(ref _totalSent);
            dic["message:received"] = Interlocked.Read(ref _totalReceived);
            dic["message:sendSize"] = Interlocked.Read(ref _totalSentSize);
            dic["message:recvSize"] = Interlocked.Read(ref _totalRecvSize);
            // dump out all statistics
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(OutputFile, true))
            {
                file.WriteLine(JsonConvert.SerializeObject(new
                {
                    Time = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ"),
                    Counters = dic
                }));
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
