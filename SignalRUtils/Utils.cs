using System;
using System.Collections.Generic;
using System.Text;

namespace SignalRUtils
{
    public class Utils
    {
        public static long Timestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
            //var unixDateTime = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            //return unixDateTime;
        }
    }
}
