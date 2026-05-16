using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteViewerApp.DTOs
{
    public class ControlRejectedDto
    {
        public string SessionId { get; set; } = "";
        public string HostId { get; set; } = "";
        public string ViewerId { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Reason { get; set; }
        public DateTime RejectedAt { get; set; }
    }
}
