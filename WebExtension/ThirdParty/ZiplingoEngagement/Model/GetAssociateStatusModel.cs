using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebExtension.ThirdParty.ZiplingoEngagement.Model
{
    public class GetAssociateStatusModel
    {
        public DateTime last_modified { get; set; }
        public int AssociateID { get; set; }
        public int CurrentStatusId { get; set; }
        public string StatusName { get; set; }
    }
}
