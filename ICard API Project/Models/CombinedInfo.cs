using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICard_API_Project.Models
{
    internal class CombinedInfo
    {
        public string iccid { get; set; } = "";
        public SessionDetails? session { get; set; }
        public DeviceUsage? usage { get; set; }
        public DeviceLocation? locations { get; set; }
    }
}
