using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebExtension.ThirdParty.ZiplingoEngagement.Model
{
    public class TRCPointNotificationModel
    {
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string CompanyName { get; set; }
        public string WebAlias { get; set; }
        public string LogoUrl { get; set; }
        public string CompanyUrl { get; set; }
        public int OrderId { get; set; }
        public double OrderAmount { get; set; }
        public string EmailAddress { get; set; }
        public string AssociateId { get; set; }
        public string Description { get; set; }
        public string OrderCreated { get; set; }
    }
}
