using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GridTrace.Commands;
using GridTrace.Data;
using GridTrace.Models;

namespace GridTrace.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DeviceRepository _repository = new();
    private DeviceViewModel? _selectedDevice;
    private string _statusMessage = "Ready.";

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged(nameof(SelectedDevice));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public ICommand SimulateOutageCommand { get; }
    public ICommand RestorePowerCommand { get; }

    public MainViewModel()
    {
        SimulateOutageCommand = new RelayCommand(async _ => await SimulateOutageAsync(), _ => SelectedDevice != null);
        RestorePowerCommand = new RelayCommand(async _ => await RestorePowerAsync());
    }

    public async Task LoadDevicesAsync()
    {
        try
        {
            var devices = await _repository.GetAllDevicesAsync();
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(new DeviceViewModel(device));
            }
            StatusMessage = $"Loaded {Devices.Count} devices.";
        }
        catch (Exception)
        {
            StatusMessage = "Cannot connect to Oracle at localhost:1521/XEPDB1 — is the container running?";
        }
    }

    private async Task SimulateOutageAsync()
    {
        if (SelectedDevice == null) return;

        try
        {
            var affectedIds = await _repository.GetDownstreamTraceIdsAsync(SelectedDevice.Id);
            await _repository.SetStatusAsync(affectedIds, "OUTAGE");

            var affectedSet = affectedIds.ToHashSet();
            foreach (var device in Devices)
            {
                if (affectedSet.Contains(device.Id))
                {
                    device.Status = "OUTAGE";
                }
            }

            var meterCount = Devices.Count(d => affectedSet.Contains(d.Id) && d.DeviceType == "METER");
            StatusMessage = $"{affectedIds.Count} devices affected, {meterCount} customer meters without power.";
        }
        catch (Exception)
        {
            StatusMessage = "Cannot connect to Oracle at localhost:1521/XEPDB1 — is the container running?";
        }
    }

    private async Task RestorePowerAsync()
    {
        try
        {
            await _repository.ResetAllStatusAsync();
            foreach (var device in Devices)
            {
                device.Status = "NORMAL";
            }
            StatusMessage = "Power restored across all devices.";
        }
        catch (Exception)
        {
            StatusMessage = "Cannot connect to Oracle at localhost:1521/XEPDB1 — is the container running?";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}