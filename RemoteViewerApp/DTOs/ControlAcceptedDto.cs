using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteViewerApp.DTOs
{
    public class ControlAcceptedDto
    {
        public string SessionId { get; set; } = "";
        public string HostId { get; set; } = "";
        public string ViewerId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime AcceptedAt { get; set; }
    }
}
