using System;
using System.Collections.Generic;
using System.Text;

namespace SignalRUtils
{
    public class SignalRAccessInfo
    {
        /// <summary>
        /// SignalR连接地址
        /// </summary>
        public string Url { set; get; }

        /// <summary>
        /// SignalR TOKEN
        /// </summary>
        public string Token { set; get; }

        /// <summary>
        /// token 有效期
        /// </summary>
        public long Expire { set; get; }
    }
}
