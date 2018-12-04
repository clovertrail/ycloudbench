using System;
using System.Collections.Generic;
using System.Text;

namespace SignalRUtils
{
    public class PayloadMessage
    {
        public string Target { get; set; }

        public object[] Arguments { get; set; }
    }
}
