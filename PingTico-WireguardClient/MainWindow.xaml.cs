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

namespace PingTico_WireguardClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadApplicationList();


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
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    ApplicationItem application = new ApplicationItem
                    {
                        Name = process.MainWindowTitle,
                        ExecutableName = process.ProcessName,
                        PID = process.Id,
                        Icon = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName),
                        IsSelected = false
                    };
                    applications.Add(application);
                }
            }

            ProcessListView.ItemsSource = applications;
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

                                ConnectionNameLbl.Content = "-";
                                AddressLbl.Content = "-";
                                EndpointLbl.Content = "-";
                                StatusLbl.ToolTip = null;

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
                                    AddressLbl.Content = _fileConfig[i].Trim().Replace(" ", "").ToLower().Split("=").Last();
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

                        _ = Task.Run(() =>
                        {
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

        private void CheckBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void ProcessListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }
    }
}
