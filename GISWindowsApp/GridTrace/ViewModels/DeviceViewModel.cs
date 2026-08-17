using System.ComponentModel;
using System.Windows.Media;
using GridTrace.Models;

namespace GridTrace.ViewModels;

public class DeviceViewModel : INotifyPropertyChanged
{
    private readonly NetworkDevice _device;
    private string _status;

    public DeviceViewModel(NetworkDevice device)
    {
        _device = device;
        _status = device.Status;
    }

    public int Id => _device.Id;
    public string Name => _device.Name;
    public string DeviceType => _device.DeviceType;
    public int? ParentId => _device.ParentId;
    public double PosX => _device.PosX;
    public double PosY => _device.PosY;

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                _device.Status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(Fill));
            }
        }
    }

    public Brush Fill => Status == "OUTAGE" ? Brushes.Red : DeviceType switch
    {
        "SUBSTATION" => Brushes.DarkSlateGray,
        "FEEDER" => Brushes.SteelBlue,
        "POLE" => Brushes.SaddleBrown,
        "TRANSFORMER" => Brushes.Goldenrod,
        "METER" => Brushes.ForestGreen,
        _ => Brushes.Gray
    };

    public string DisplayLabel => $"{Name} ({DeviceType})";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}