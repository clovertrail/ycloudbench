﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SignalRUtils
{
    public class DelayCounter : IDisposable
    {
        private readonly static string DEFAULT_OUTPUT_FILE = "DelayCounters.txt";
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
        private readonly string OutputFile;

        private List<long> _delayList;

        private Timer _timer;
        private long _startPrint;
        private bool _hasRecord;
        private object _fileLock = new object();

        public DelayCounter(string outputFile = null)
        {
            OutputFile = outputFile != null ? outputFile : DEFAULT_OUTPUT_FILE;
            _delayList = new List<long>();
        }

        public void Add(long delay)
        {
            lock (_delayList)
            {
                _delayList.Add(delay);
            }
            _hasRecord = true;
        }

        public void Dispose()
        {
            _timer?.Dispose();
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
                ((DelayCounter)state).InternalReportDistribution();
                _hasRecord = false;
            }
        }

        private void InternalReportDistribution()
        {
            var arrCopy = new List<long>();
            lock (_delayList)
            {
                arrCopy.AddRange(_delayList);
            }
            arrCopy.Sort();
            var len = arrCopy.Count;
            int percentage99 = (int)(len * 0.99);
            int percentage95 = (int)(len * 0.95);
            int percentage90 = (int)(len * 0.9);

            lock (_fileLock)
            {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(OutputFile, true))
                {
                    file.WriteLine($"{DateTimeOffset.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")}: reconnect: {len}," +
                    $" 99% takes less than {arrCopy[percentage99]} ms," +
                    $" 95% takes less than {arrCopy[percentage95]} ms," +
                    $" 90% takes less than {arrCopy[percentage90]} ms");
                }
            }
        }
    }
}
