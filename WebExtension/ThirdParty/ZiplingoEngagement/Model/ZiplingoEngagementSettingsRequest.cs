using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebExtension.ThirdParty.ZiplingoEngagement.Model;

namespace WebExtension.ThirdParty.Model
{
    public class ZiplingoEngagementSettingsRequest : CommandRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string ApiUrl { get; set; }
        public string LogoUrl { get; set; }
        public string CompanyName { get; set; }
        public bool AllowBirthday { get; set; }
        public bool AllowAnniversary { get; set; }
        public bool AllowRankAdvancement { get; set; }
    }
}
