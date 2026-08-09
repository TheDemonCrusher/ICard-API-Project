using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICard_API_Project.Models
{
    internal class CombinedInfo
    {
        public string icid { get; set; } = "";
        public SessionDetails? details { get; set; }
        public DeviceUsage? usage { get; set; }
        public DeviceLocation? location { get; set; }
    }
}
