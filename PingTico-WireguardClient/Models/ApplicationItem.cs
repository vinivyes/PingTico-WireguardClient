using PingTico.Helpers;
using PingTico_WireguardClient.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PingTico_WireguardClient.Models;
public class ApplicationItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private bool _isSelected, _isEnabled;
    private Visibility _visibility;
    private SplitTunnelingConfig _SplitTunnelingConfig;

    public string Name { get; set; }
    public string ExecutableName { get; set; }
    public int PID { get; set; }
    public Icon Icon { get; set; }
    public Visibility Visibility
    {
        get { return _visibility; }
        set
        {
            _visibility = value;
            OnPropertyChanged("Visibility");
        }
    }

    public SplitTunnelingConfig SplitTunnelingConfig { 
        get
        {
            return _SplitTunnelingConfig;
        }
        set
        {
            _SplitTunnelingConfig = value;
            StartSplitTunneling();
        }
    }
    public Process? SplitTunnelingProcess { get; set; }

    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            if (!Wireguard.isConnected)
                return;

            _isSelected = value;

            if(value)
                StartSplitTunneling();
            else
                StopSplitTunneling();

            OnPropertyChanged("IsSelected");
        }
    }
    public bool IsEnabled
    {
        get { return _isEnabled; }
        set
        {
            _isEnabled = value;
            OnPropertyChanged("IsEnabled");
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void StopSplitTunneling()
    {
        if (SplitTunnelingProcess != null)
        {
            try
            {
                //If a process is running, kill it
                SplitTunnelingProcess.Kill();
                SplitTunnelingProcess.Dispose();

                SplitTunnelingProcess = null;
            }
            catch
            {
                SplitTunnelingProcess = null;
            }
        }
    }

    public void StartSplitTunneling()
    {
        if (!Wireguard.isConnected || !IsSelected)
            return;

        if(SplitTunnelingProcess != null)
        {
            try
            {
                //If a process is running, kill it
                SplitTunnelingProcess.Kill();
                SplitTunnelingProcess.Dispose();

                SplitTunnelingProcess = null;
            }
            catch
            {
                SplitTunnelingProcess = null;
            }
        }

        int currentAdapterIndex = Utils.Utils.GetWireguardInterfaceID("pingtico") ?? -1;
        int retry = 0;
        while (currentAdapterIndex == -1 && retry < 3)
        {
            Thread.Sleep(500);
            retry++;
        }

        if(currentAdapterIndex == -1)
        {
            IsSelected = false;
            return;
        }

        bool anyProtocol = SplitTunnelingConfig.ProtocolTcp && SplitTunnelingConfig.ProtocolUdp;
        string args = $"--block \"processId == {PID} {(anyProtocol ? "" : (SplitTunnelingConfig.ProtocolTcp ? "and tcp" : "and udp"))} and (localAddr != {Wireguard.localAddress.Split('/')[0]} and localAddr != :: and localAddr != 127.0.0.1 {(SplitTunnelingConfig.PortRanges.Count > 0 ? $"and ({string.Join(" or ",SplitTunnelingConfig.PortRanges.Select(s => $"(remotePort >= {s.Start} and remotePort <= {s.End})"))})" : "")})\" {currentAdapterIndex}";

        //Process options
        ProcessStartInfo processInfo = new ProcessStartInfo();

        processInfo.FileName = Utils.Utils.LocalPath("socketdump.exe");
        processInfo.Arguments = args;
        processInfo.UseShellExecute = false;
        processInfo.CreateNoWindow = true;

        //Starts the Multi Path Client
        SplitTunnelingProcess = new Process();
        SplitTunnelingProcess.StartInfo = processInfo;
        SplitTunnelingProcess.EnableRaisingEvents = true;

        SplitTunnelingProcess.Start();

        //Will make sure all of the processes get killed on disconnection or termination of the application
        ChildProcessTracker.AddProcess(SplitTunnelingProcess);
    }
}
