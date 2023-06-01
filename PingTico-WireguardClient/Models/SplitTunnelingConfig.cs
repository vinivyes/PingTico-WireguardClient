using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PingTico_WireguardClient.Models
{
    public class SplitTunnelingConfig
    {
        public SplitTunnelingConfig()
        {

        }

        public SplitTunnelingConfig(SplitTunnelingConfig s)
        {
            var properties = typeof(SplitTunnelingConfig).GetProperties();
            foreach (var property in properties)
            {
                try
                {
                    if (property.CanWrite)
                    {
                        property.SetValue(this, property.GetValue(s));
                    }
                }
                catch { }
            }
        }
        public string ExecutableName { get; set; } = "*";
        public List<PortRange> PortRanges { get; set; } = new List<PortRange>();
        public bool ProtocolUdp { get; set; }
        public bool ProtocolTcp { get; set; }
    }

    public class PortRange
    {
        public int Start { get; set; }
        public int End { get; set; }

        public override string ToString()
        {
            return $"{Start}-{End}";
        }
    }
}
