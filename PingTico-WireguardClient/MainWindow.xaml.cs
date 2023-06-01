using PingTico_WireguardClient.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Tunnel;
using PingTico_WireguardClient.Utils;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Threading;
using PingTico_WireguardClient.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Animation;

namespace PingTico_WireguardClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public SplitTunnelingConfig AllApplicationSplitTunnelingConfig = new SplitTunnelingConfig() { ProtocolTcp = true, ProtocolUdp = true };
        public List<SplitTunnelingConfig> StoredSplitTunnelingConfig = new List<SplitTunnelingConfig>();


        private bool initialized = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadApplicationList();

            if (File.Exists("./SplitTunnelingConfigs.json"))
            {
                try
                {
                    StoredSplitTunnelingConfig = JsonSerializer.Deserialize<List<SplitTunnelingConfig>>(File.ReadAllText("./SplitTunnelingConfigs.json")) ?? new List<SplitTunnelingConfig>();
                    AllApplicationSplitTunnelingConfig = StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == "*") ?? new SplitTunnelingConfig() { ProtocolTcp = true, ProtocolUdp = true };
                }
                catch { }
            }

            initialized = true;

            LoadSelectedSplitTunnelConfig();

            _ = Task.Run(() =>
            {
                while (true)
                {
                    TimeSpan? latestHandshake = Wireguard.isConnected ? GetHandshake() : null;
                    if (latestHandshake.HasValue)
                    {
                        Dispatcher.Invoke(new Action(
                            () =>
                                {
                                    if (Wireguard.isConnected && latestHandshake.Value.TotalMinutes > 3)
                                    {
                                        Connect();
                                        return;
                                    }

                                    StatusLbl.Content = $"Connected - Latest Handshake: {latestHandshake.Value.ToString(@"mm\:ss")}";
                                    StatusImg.Background = Brushes.Green;

                                    double? latency = Utils.Utils.PingIpAddress(
                                                        Utils.Utils.DecimalToIPAddress(
                                                            Utils.Utils.IPAddressToDecimal(Wireguard.localAddress.Split('/').FirstOrDefault() ?? "") - 1
                                                        ), 2, 250
                                                    );

                                    LatencyLbl.Content = $"{(latency.HasValue ? latency.Value.ToString("0.##") : "-")} ms";
                                }
                            )
                        );
                    }
                    else
                    {
                        Dispatcher.Invoke(new Action(
                                () =>
                                {
                                    StatusLbl.Content = "Disconnected";
                                    StatusImg.Background = Brushes.Red;
                                }
                            )
                        );
                    }
                    Thread.Sleep(1000);
                }
            });
        }
        private DateTime connectionDate = DateTime.MinValue;

        private void DragEvent(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadApplicationList();
        }

        private void LoadApplicationList()
        {
            ObservableCollection<ApplicationItem> applications = new ObservableCollection<ApplicationItem>();

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    System.Drawing.Icon i = null;
                    try { i = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName); } catch { }

                    ApplicationItem application = new ApplicationItem
                    {
                        Name = process.MainWindowTitle,
                        ExecutableName = process.ProcessName,
                        PID = process.Id,
                        Icon = i,
                        IsSelected = false,
                        Visibility = string.IsNullOrEmpty(process.MainWindowTitle) ? Visibility.Hidden : Visibility.Visible,
                        IsEnabled = Wireguard.isConnected && !string.IsNullOrEmpty(process.MainWindowTitle),
                        SplitTunnelingConfig = StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == process.ProcessName) ?? AllApplicationSplitTunnelingConfig
                    };

                    if (StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == process.ProcessName) is not null)
                        application.IsSelected = true;

                    applications.Add(application);
                }
                catch
                {

                }
            }


            foreach (ApplicationItem item in ProcessListView.ItemsSource ?? new List<ApplicationItem>())
            {
                if (applications.FirstOrDefault(a => a.PID == item.PID) is null)
                    item.StopSplitTunneling();
            }

            ProcessListView.ItemsSource = applications.OrderBy(a => a.Visibility != Visibility.Visible).Where(a => applications.Where(b => b.ExecutableName != a.ExecutableName && b.Visibility == Visibility.Visible).Count() > 0).ToList();
        }

        private void UpdateForm()
        {
            ConnectBtn.IsEnabled = (DateTime.Now - connectionDate).TotalSeconds > 2;
            ConnectBtn.Content = !IsConnected() ? "Connect" : "Disconnect";
        }

        private void Connect()
        {
            if (IsConnected())
            {
                ConnectBtn.IsEnabled = false;
                ConnectBtn.Content = "Disconnecting...";

                _ = Task.Run(() =>
                {
                    Dispatcher.Invoke(new Action(
                        () =>
                            {
                                Service.Remove(Utils.Utils.LocalPath("pingtico.conf"), true);
                                Wireguard.isConnected = false;

                                if (threadsRunning)
                                {
                                    threadsRunning = false;
                                    transferUpdateThread.Interrupt();
                                }

                                ConnectionNameLbl.Content = "-";
                                AddressLbl.Content = "-";
                                EndpointLbl.Content = "-";

                                LatencyLbl.Content = "-";

                                DownloadLbl.Content = "-";
                                UploadLbl.Content = "-";

                                StatusLbl.ToolTip = null;

                                foreach (ApplicationItem item in ProcessListView.ItemsSource)
                                {
                                    item.IsEnabled = false;
                                    item.StopSplitTunneling();
                                }

                                UpdateForm();
                            }
                        )
                    );
                });
            }
            else
            {
                try
                {
                    ConnectBtn.IsEnabled = false;
                    ConnectBtn.Content = "Connecting...";

                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

                    dialog.DefaultExt = ".conf";
                    dialog.Filter = "Wireguard CONF Files (*.conf)|*.conf";

                    Nullable<bool> result = dialog.ShowDialog();

                    if (result == true)
                    {
                        string filename = dialog.FileName;

                        string[] fileConfig = File.ReadAllLines(filename);
                        fileConfig = fileConfig.Where(l => !string.IsNullOrEmpty(l)).ToArray();

                        bool hasTableOff = false;
                        foreach (string line in fileConfig)
                            if (line.Trim().Replace(" ", "").ToLower() == "table=off")
                                hasTableOff = true;

                        if (!hasTableOff)
                        {
                            List<string> _fileConfig = fileConfig.ToList();
                            for (int i = 0; i < _fileConfig.Count; i++)
                            {
                                if (_fileConfig[i].Trim().Replace(" ", "").ToLower() == "[peer]")
                                {
                                    _fileConfig.Insert(i - 1, "Table=off");
                                    i++;
                                }
                                if (_fileConfig[i].Trim().Replace(" ", "").ToLower().Split("=").Contains("address"))
                                {
                                    Wireguard.localAddress = _fileConfig[i].Trim().Replace(" ", "").ToLower().Split("=").Last();
                                    AddressLbl.Content = Wireguard.localAddress;
                                }
                                if (_fileConfig[i].Trim().Replace(" ", "").ToLower().Split("=").Contains("endpoint"))
                                {
                                    EndpointLbl.Content = _fileConfig[i].Trim().Replace(" ", "").ToLower().Split("=").Last();
                                }
                            }
                                
                            fileConfig = _fileConfig.ToArray();
                        }

                        File.WriteAllText(Utils.Utils.LocalPath("pingtico.conf"), string.Join(Environment.NewLine, fileConfig.Select(l => l.Trim()).ToArray()));

                        Service.Add(Utils.Utils.LocalPath("pingtico.conf"), false);
                        Wireguard.isConnected = true;

                        connectionDate = DateTime.Now;

                        ConnectionNameLbl.Content = filename.Split('\\').Last();


                        UpdateForm();

                        if (!threadsRunning)
                        {
                            //Update Transfer rate
                            transferUpdateThread = new Thread(new ThreadStart(tailTransfer));
                            threadsRunning = true;
                            transferUpdateThread.Start();
                        }

                        _ = Task.Run(() =>
                        {
                            foreach (ApplicationItem item in ProcessListView.ItemsSource)
                            {

                                if (StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == item.ExecutableName) is not null)
                                    item.IsSelected = true;

                                item.IsEnabled = true;
                            }

                            Dispatcher.Invoke(new Action(
                                () =>
                                    {

                                        Thread.Sleep(3000);
                                        UpdateForm();
                                    }
                                )
                            );
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error while connecting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateForm();
                }
            }
        }

        private bool IsConnected()
        {
            try
            {
                Service.GetAdapter("pingtico");
                return true;
            }
            catch { return false; }
        }

        public TimeSpan? GetHandshake()
        {
            try
            {
                DateTime latestHandshake = Service.GetAdapter("pingtico.conf").GetConfiguration().Peers.Last().LastHandshake;
                if(latestHandshake < DateTime.UtcNow.AddDays(-1))
                    return null;

                return (DateTime.UtcNow - latestHandshake);
            }
            catch { return null; }
        }


        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void MinimizeBtn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseBtn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Environment.Exit(0);
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;

            if (checkBox != null && checkBox.IsChecked == true)
            {
                if(MessageBox.Show("We do not recommend removing split-tunnel to prevent connection issues, instead, disconnect the VPN. Are you sure you want to continue ?","Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    e.Handled = true;
                }
                else
                {
                    checkBox.IsChecked = false;
                }
            }
        }

        private void ProcessListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSelectedSplitTunnelConfig();
        }

        private bool loadingConfig = false;
        private void LoadSelectedSplitTunnelConfig()
        {
            loadingConfig = true;
            SplitTunnelingConfig sptc = StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == (ProcessListView.SelectedIndex == -1 ? "*" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName));
            SaveCb.IsChecked = sptc is not null;

            bool anyProtocol = sptc is null ? true : sptc.ProtocolTcp && sptc.ProtocolUdp;
            if (anyProtocol)
                ProtAnyRd.IsChecked = true;
            else if (sptc.ProtocolTcp)
                ProtTCPRd.IsChecked = true;
            else if (sptc.ProtocolUdp)
                ProtUDPRd.IsChecked = true;
            else
                ProtAnyRd.IsChecked = true;

            PortsLb.ItemsSource = sptc is null ? new List<PortRange>() : sptc.PortRanges;

            ApplicationLbl.Content = ProcessListView.SelectedIndex == -1 ? "All" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName;
            loadingConfig = false;
        }

        #region - Transfer Rate Logic

        private Tunnel.Ringlogger log;
        private Thread transferUpdateThread;
        private volatile bool threadsRunning;
        private void tailTransfer()
        {
            Tunnel.Driver.Adapter adapter = null;
            while (threadsRunning || Wireguard.isConnected)
            {
                if (adapter == null)
                {
                    while (threadsRunning)
                    {
                        try
                        {
                            adapter = Tunnel.Service.GetAdapter(Utils.Utils.LocalPath("pingtico.conf"));
                            break;
                        }
                        catch
                        {
                            try
                            {
                                Thread.Sleep(1000);
                            }
                            catch { }
                        }
                    }
                }
                if (adapter == null)
                    continue;
                try
                {
                    ulong rx = 0, tx = 0;
                    var config = adapter.GetConfiguration();
                    foreach (var peer in config.Peers)
                    {
                        rx += peer.RxBytes;
                        tx += peer.TxBytes;
                    }
                    updateTransferValues(rx, tx);
                    Thread.Sleep(1000);
                }
                catch { adapter = null; }
            }
        }

        ulong rx_total = 0;
        ulong tx_total = 0;
        private void updateTransferValues(ulong rx, ulong tx)
        {
            try
            {
                if (!Wireguard.isConnected) { return; }

                ulong rx_difference = rx - rx_total;
                ulong tx_difference = tx - tx_total;

                rx_total = rx;
                tx_total = tx;

                Dispatcher.Invoke(new Action(
                () =>
                {
                    if (Wireguard.isConnected)
                    {
                        DownloadLbl.Content = (rx_difference / 1024) + " kbps";
                        UploadLbl.Content = (tx_difference / 1024)  + " kbps";
                    }
                }
                ));

            }
            catch {}
        }

        #endregion - Transfer Rate Logic
        
        public static bool ValidatePortInput(string input)
        {
            Regex regex = new Regex(@"^\d+(-\d*)?$");

            if (regex.IsMatch(input))
            {
                try
                {
                    int[] parts = input.Split('-').Where(p => !string.IsNullOrEmpty(p)).Select(p => int.Parse(p)).ToArray();
                    int startPort = parts.Min();
                    int endPort = parts.Length > 1 ? parts.Max() : startPort;

                    if (startPort >= 1 && startPort <= 65535 && endPort >= startPort && endPort <= 65535)
                    {
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private void PortTb_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            // Only allow digits and valid port range characters
            Regex regex = new Regex(@"^[0-9-]$");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true; // Suppress the input if it doesn't match the pattern
            }

            // Additional validation for the complete input after the new character is entered
            string updatedText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            if (!ValidatePortInput(updatedText))
            {
                e.Handled = true; // Suppress the input if it's not a valid port input
            }
        }

        private void PortTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            AddPortBtn.IsEnabled = !string.IsNullOrEmpty(PortTb.Text);
        }

        private void SaveCb_Checked(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void SaveCb_Unchecked(object sender, RoutedEventArgs e)
        {
            RemoveConfig();
        }

        private void AddPortBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PortTb.Text.Length == 0)
                return;

            List<PortRange> ports = PortsLb.ItemsSource is null ? new List<PortRange>() : PortsLb.ItemsSource.Cast<PortRange>().ToList();

            int[] parts = PortTb.Text.Split('-').Where(p => !string.IsNullOrEmpty(p)).Select(p => int.Parse(p)).ToArray();
            int startPort = parts.Min();
            int endPort = parts.Length > 1 ? parts.Max() : startPort;

            ports.Add(new PortRange() { Start = startPort, End = endPort });

            PortsLb.ItemsSource = ports;

            PortTb.Text = "";

            SaveConfig();
        }

        private void SaveConfig()
        {
            if (!initialized || loadingConfig)
                return;

            SplitTunnelingConfig? sptc = StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == (ProcessListView.SelectedIndex == -1 ? "*" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName));
            if (sptc is null)
            {
                sptc = new SplitTunnelingConfig() { ExecutableName = (ProcessListView.SelectedIndex == -1 ? "*" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName) };
            }

            sptc.ProtocolUdp = (ProtUDPRd.IsChecked ?? false) || (ProtAnyRd.IsChecked ?? false);
            sptc.ProtocolTcp = (ProtTCPRd.IsChecked ?? false) || (ProtAnyRd.IsChecked ?? false);

            sptc.PortRanges = PortsLb.ItemsSource is null ? new List<PortRange>() : PortsLb.ItemsSource.Cast<PortRange>().ToList();

            StoredSplitTunnelingConfig = StoredSplitTunnelingConfig.Where(sp => sp.ExecutableName != sptc.ExecutableName).ToList();
            StoredSplitTunnelingConfig.Add(sptc);


            foreach (ApplicationItem item in ProcessListView.ItemsSource)
            {
                if (sptc.ExecutableName != "*" && item.ExecutableName != sptc.ExecutableName && item.ExecutableName != "*")
                    continue;

                item.SplitTunnelingConfig = sptc;
                if (item.IsSelected)
                {
                    item.StartSplitTunneling();
                }
            }

            SaveCb.IsChecked = true;

            File.WriteAllBytes("./SplitTunnelingConfigs.json", JsonSerializer.SerializeToUtf8Bytes(StoredSplitTunnelingConfig));
        }

        private void RemoveConfig()
        {
            if (!initialized || loadingConfig)
                return;

            SplitTunnelingConfig? sptc = StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == (ProcessListView.SelectedIndex == -1 ? "*" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName));
            if (sptc is null)
            {
                sptc = new SplitTunnelingConfig() { ExecutableName = (ProcessListView.SelectedIndex == -1 ? "*" : ((ApplicationItem)ProcessListView.SelectedItem).ExecutableName) };
            }

            StoredSplitTunnelingConfig = StoredSplitTunnelingConfig.Where(sp => sp.ExecutableName != sptc.ExecutableName).ToList();

            foreach (ApplicationItem item in ProcessListView.ItemsSource)
            {
                if (item.ExecutableName != sptc.ExecutableName)
                    continue;

                item.SplitTunnelingConfig = AllApplicationSplitTunnelingConfig;
                if (item.IsSelected)
                {
                    item.StartSplitTunneling();
                }
            }

            SaveCb.IsChecked = false;

            File.WriteAllBytes("./SplitTunnelingConfigs.json", JsonSerializer.SerializeToUtf8Bytes(StoredSplitTunnelingConfig));
        }

        private void ProtAnyRd_Checked(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void ProtAnyRd_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void PortsLb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemovePortBtn.IsEnabled = PortsLb.SelectedIndex != -1;
        }

        private void RemovePortBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PortsLb.SelectedIndex == -1)
                return;

            List<PortRange> portRanges = PortsLb.ItemsSource is null ? new List<PortRange>() : PortsLb.ItemsSource.Cast<PortRange>().ToList();
            portRanges = portRanges.Where(pr => pr.Start != ((PortRange)PortsLb.SelectedItem).Start && pr.End != ((PortRange)PortsLb.SelectedItem).End).ToList();

            PortsLb.ItemsSource = portRanges;

            SaveConfig();
        }

        private bool ignoreCascade = false;
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ignoreCascade)
                return;

            ignoreCascade = true;

            _ = Task.Run(() =>
            {
                try
                {
                    CheckBox cb = sender as CheckBox;
                    ApplicationItem check = cb.DataContext as ApplicationItem;

                    foreach (ApplicationItem item in ProcessListView.ItemsSource)
                    {
                        if (StoredSplitTunnelingConfig.FirstOrDefault(s => s.ExecutableName == item.ExecutableName) is not null || check is not null ? check.ExecutableName == item.ExecutableName : false)
                            item.IsSelected = true;
                    }
                }
                catch { }
            });

            ignoreCascade = false;
        }
    }
}
