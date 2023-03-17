using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebExtension.Helper
{
    public class TokenRequest
    {
        public string grant_type { get; set; } = "password";
        public string username { get; set; }
        public string password { get; set; }
        public string client_id { get; set; }
    }
}
