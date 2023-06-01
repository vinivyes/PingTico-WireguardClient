using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PingTico_WireguardClient.Utils;

public static class Utils
{
    /// <summary>
    /// Gets the full path of where the application is running from
    /// </summary>
    /// <param name="localFile">The path to a file located in the local application folder</param>
    /// <returns></returns>
    public static string LocalPath(string localFile = null)
    {
        return
            localFile != null ?
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", localFile) :
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "");
    }

    /// <summary>
    /// Converts an IP address string to its decimal representation.
    /// </summary>
    /// <param name="ipAddressString">The IP address string to convert.</param>
    /// <returns>The decimal representation of the IP address.</returns>
    public static uint IPAddressToDecimal(string ipAddressString)
    {
        IPAddress ipAddress = IPAddress.Parse(ipAddressString);
        byte[] ipAddressBytes = ipAddress.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(ipAddressBytes);
        }
        return BitConverter.ToUInt32(ipAddressBytes, 0);
    }

    /// <summary>
    /// Converts a decimal representation of an IP address to its IP address string.
    /// </summary>
    /// <param name="ipAddressDecimal">The decimal representation of the IP address.</param>
    /// <returns>The IP address string.</returns>
    public static string DecimalToIPAddress(uint ipAddressDecimal)
    {
        byte[] ipAddressBytes = BitConverter.GetBytes(ipAddressDecimal);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(ipAddressBytes);
        }
        return new IPAddress(ipAddressBytes).ToString();
    }

    public static double? PingIpAddress(string ipAddress, int pingCount = 4, int timeout = 300)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        if (pingCount < 1)
        {
            return null;
        }

        if (timeout < 1)
        {
            return null;
        }

        IPAddress address;
        if (!IPAddress.TryParse(ipAddress, out address))
        {
            return null;
        }

        try
        {

            double totalLatency = 999;
            using (Ping ping = new Ping())
            {
                for (int i = 0; i < pingCount; i++)
                {
                    PingReply reply = ping.SendPingAsync(address, timeout).Result;
                    if (reply.Status == IPStatus.Success)
                    {
                        totalLatency = reply.RoundtripTime < totalLatency ? reply.RoundtripTime : totalLatency;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return totalLatency;
        }
        catch
        { return null; }

    }


    public static int? GetWireguardInterfaceID(string adapterName)
    {

        int currentAdapterIndex = -1;
        int retryInterfaceSearch = 0;

        bool found = false;

        while (!found && retryInterfaceSearch < 10)
        {

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.Name == adapterName)
                {
                    found = true;
                    currentAdapterIndex = adapter.GetIPProperties().GetIPv4Properties().Index;
                }
                else
                {
                    retryInterfaceSearch++;
                }
            }

            Thread.Sleep(500);
        }

        if (!found)
        {
            return null;
        }

        return currentAdapterIndex;
    }

}