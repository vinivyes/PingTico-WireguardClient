using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingTico_WireguardClient.Models;
public class ApplicationItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private bool _isSelected;

    public string Name { get; set; }
    public string ExecutableName { get; set; }
    public int PID { get; set; }
    public Icon Icon { get; set; }
    public Process? splitTunnelling { get; set; }
    
    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            _isSelected = value;
            OnPropertyChanged("IsSelected");
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
