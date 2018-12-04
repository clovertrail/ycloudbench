using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRGameServer.Models
{
    public class AuthOptions
    {
        public string SigningKey { set; get; }

        public string Issuer { set; get; } = "ChatJwt";

        public string Audience { set; get; } = "ChatJwt";
    }
}
