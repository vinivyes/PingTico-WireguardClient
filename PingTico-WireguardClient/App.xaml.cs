using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace PingTico_WireguardClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {

        if (e.Args.Length == 3 && e.Args[0] == "/service")
        {
            var t = new Thread(() =>
            {
                try
                {
                    Process currentProcess = Process.GetCurrentProcess();
                    Process uiProcess = Process.GetProcessById(int.Parse(e.Args[2]));
                    if (uiProcess.MainModule.FileName != currentProcess.MainModule.FileName)
                        return;
                    uiProcess.WaitForExit();
                    Tunnel.Service.Remove(e.Args[1], false);
                }
                catch { }
            });
            t.Start();
            Tunnel.Service.Run(e.Args[1]);
            t.Interrupt();
            return;
        }

        //Make sure we're running as Administrator
        if (!IsAdministrator())
        {
            if (e.Args.Contains("--elevatedProcess")) { 
                MessageBox.Show("This application must run as administrator", "Elevation Error", MessageBoxButton.OK, MessageBoxImage.Error); 
                Environment.Exit(-1); 
                return; 
            }

            // Restart program and run as admin
            var exeName = Environment.ProcessPath;

            if(exeName is null)
                Environment.Exit(-2);

            ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
            startInfo.Verb = "runas";
            startInfo.Arguments = "--elevatedProcess";
            System.Diagnostics.Process.Start(startInfo);
            Environment.Exit(-3);
            return;
        }

        EnsureUniqueInstance(e.Args.Contains("--elevatedProcess"));

        InstallWireguardTunnel();
        InstallWinDivertTunnelingService();

    }

    private static bool IsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void EnsureUniqueInstance(bool elevated)
    {

        var current = Process.GetCurrentProcess();

        if (Process.GetProcessesByName(current.ProcessName).Length > (elevated ? 2 : 1))
        {
            if (MessageBox.Show("Other instances of this application are already running. \nYou need to close the other instances to continue, would you like to force close them ?", "Ya en ejecución", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Process.GetProcessesByName(current.ProcessName)
                    .Where(t => t.Id != current.Id)
                    .ToList()
                    .ForEach(t => t.Kill());
            }
            else
            {
                Environment.Exit(-1);
            }
        }
    }

    public static void InstallWinDivertTunnelingService()
    {
        try
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("SystemRoot") + @"\system32\drivers\windivert64.sys"))
            {
                System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
                pProcess.StartInfo.FileName = "rundll32";
                pProcess.StartInfo.Arguments = @"syssetup,SetupInfObjectInstallAction DefaultInstall 128 .\windivert64.inf";

                pProcess.StartInfo.UseShellExecute = false;

                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.CreateNoWindow = true;

                pProcess.Start();
                string strOutput = pProcess.StandardOutput.ReadToEnd();
                pProcess.WaitForExit();

                MessageBox.Show("Split tunneling service has been installed, it is recommended to restart before proceeding", "Split Tunelling Service", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                return;
            }
        }
        catch
        {
            if (MessageBox.Show("Split tunneling service installation failed \nWould you like to try again ?", "WinDivert Service Installation Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                InstallWinDivertTunnelingService();
            }
            else
            {
                Environment.Exit(-4);
            }
        }
    }

    public static string current_wg_version = "0.5.3";

    private static void InstallWireguardTunnel()
    {
        try
        {
            if (File.Exists(@"C:\Program Files\WireGuard\wireguard.exe") && (File.Exists("./wg_version") ? File.ReadAllText("./wg_version") == current_wg_version : false)) { return; }

            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
            pProcess.StartInfo.FileName = "msiexec.exe";
            pProcess.StartInfo.Arguments = "/q /i wg.msi";

            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.CreateNoWindow = true;

            pProcess.Start();
            pProcess.WaitForExit();

            File.WriteAllText("./wg_version", current_wg_version);

            Thread.Sleep(1000);

            pProcess = new System.Diagnostics.Process();
            pProcess.StartInfo.FileName = @"wireguard";
            pProcess.StartInfo.Arguments = @"/uninstallmanagerservice";

            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.CreateNoWindow = true;

            pProcess.Start();
            pProcess.WaitForExit();

        }
        catch{}

        try
        {
            // Restart program and run as admin
            var exeName = Environment.ProcessPath;

            if (exeName is null)
                Environment.Exit(-2);

            ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
            startInfo.Verb = "runas";
            System.Diagnostics.Process.Start(startInfo);
            Environment.Exit(-1);
        }
        catch { }
    }


}
