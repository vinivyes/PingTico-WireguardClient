using PingTico_WireguardClient.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        }

        private void DragEvent(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
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
    }
}
