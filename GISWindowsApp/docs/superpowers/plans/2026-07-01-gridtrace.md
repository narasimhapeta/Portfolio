# GridTrace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Note:** This plan is intended to be followed manually by the project owner, step by step, rather than executed by an agent. Steps and code snippets are written to be copy-pasteable.

**Goal:** Build a WPF (.NET 8) desktop app that emulates Hexagon Intergraph G/Technology's connectivity-based outage impact tracing over an electric distribution network model, backed by Oracle.

**Architecture:** Single WPF project, MVVM pattern, no separate API layer. Raw ADO.NET (`Oracle.ManagedDataAccess.Core`) talks directly to a self-referencing `network_devices` table in the already-running `oracle-spatial` Oracle XE container. The outage trace uses Oracle's native `START WITH ... CONNECT BY PRIOR` hierarchical query. The network is rendered as a schematic diagram on a WPF `Canvas` (no map tiles).

**Tech Stack:** .NET 8 (WPF), Oracle XE 21c (Docker, already running), Oracle.ManagedDataAccess.Core, xUnit.

See `docs/superpowers/specs/2026-07-01-gridtrace-design.md` for full design rationale.

---

## Global Constraints

- .NET version: 8.0 (net8.0-windows, WPF)
- Oracle: reuse running `oracle-spatial` container, port 1521, service `XEPDB1`
- Connection string: `User Id=system;Password=OraPassword1;Data Source=localhost:1521/XEPDB1;`
- Data access: `Oracle.ManagedDataAccess.Core` only — no EF Core
- No authentication; table created manually via SQL*Plus
- MVVM pattern, hand-rolled `RelayCommand` (no MVVM toolkit dependency)
- Schematic diagram only — no geographic map/tiles (see design spec §"Out of Scope")

---

## Project File Map

```
GISWindowsApp/
├── GridTrace.sln
├── src/
│   └── GridTrace/
│       ├── GridTrace.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── Models/
│       │   └── NetworkDevice.cs
│       ├── Commands/
│       │   └── RelayCommand.cs
│       ├── Data/
│       │   └── DeviceRepository.cs
│       ├── ViewModels/
│       │   ├── DeviceViewModel.cs
│       │   └── MainViewModel.cs
│       └── Rendering/
│           └── SchematicRenderer.cs
├── tests/
│   └── GridTrace.Tests/
│       ├── GridTrace.Tests.csproj
│       └── TraceQueryTests.cs
├── README.md
└── docs/
    └── superpowers/
        ├── specs/2026-07-01-gridtrace-design.md
        └── plans/2026-07-01-gridtrace.md
```

---

## HOUR 1 — Database & Solution Scaffold (0:00–1:00)

### Task 1: Create Oracle Table and Seed Data (25 min)

**Goal:** `network_devices` table with 25 seeded rows forming a 5-level radial tree (substation → 2 feeders → 6 poles → 6 transformers → 10 meters), each with pre-computed schematic `pos_x`/`pos_y`.

- [ ] **Step 1.1 — Connect to Oracle**
  ```
  docker exec -it oracle-spatial sqlplus system/OraPassword1@XEPDB1
  ```

- [ ] **Step 1.2 — Create the table**
  ```sql
  CREATE TABLE network_devices (
    id           NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name         VARCHAR2(100) NOT NULL,
    device_type  VARCHAR2(20) NOT NULL,
    parent_id    NUMBER REFERENCES network_devices(id),
    pos_x        NUMBER,
    pos_y        NUMBER,
    status       VARCHAR2(10) DEFAULT 'NORMAL'
  );
  ```

- [ ] **Step 1.3 — Seed the tree** (insert in this exact order — IDs 1–25 are assigned sequentially, and later rows reference earlier IDs as `parent_id`)
  ```sql
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Oncor Substation A', 'SUBSTATION', NULL, 450, 50);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Feeder 1', 'FEEDER', 1, 200, 150);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Feeder 2', 'FEEDER', 1, 700, 150);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 101', 'POLE', 2, 80, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 102', 'POLE', 2, 200, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 103', 'POLE', 2, 320, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 201', 'POLE', 3, 580, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 202', 'POLE', 3, 700, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Pole 203', 'POLE', 3, 820, 250);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-101', 'TRANSFORMER', 4, 80, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-102', 'TRANSFORMER', 5, 200, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-103', 'TRANSFORMER', 6, 320, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-201', 'TRANSFORMER', 7, 580, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-202', 'TRANSFORMER', 8, 700, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('XFMR-203', 'TRANSFORMER', 9, 820, 350);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-1001', 'METER', 10, 60, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-1002', 'METER', 10, 100, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-1011', 'METER', 11, 200, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-1021', 'METER', 12, 300, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-1022', 'METER', 12, 340, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-2001', 'METER', 13, 560, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-2002', 'METER', 13, 600, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-2011', 'METER', 14, 700, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-2021', 'METER', 15, 800, 450);
  INSERT INTO network_devices (name, device_type, parent_id, pos_x, pos_y) VALUES ('Meter-2022', 'METER', 15, 840, 450);
  COMMIT;
  ```

- [ ] **Step 1.4 — Verify seed data**
  ```sql
  SELECT COUNT(*) FROM network_devices;
  ```
  Expected: `25`

- [ ] **Step 1.5 — Verify the hierarchical trace query works**
  ```sql
  SELECT id, name FROM network_devices
  START WITH id = 2
  CONNECT BY PRIOR id = parent_id;
  ```
  Expected: 12 rows (Feeder 1 + its 3 poles + 3 transformers + 5 meters). Type `exit` to leave SQL*Plus.

---

### Task 2: Scaffold the .NET Solution (20 min)

**Goal:** Solution with a WPF app project and an xUnit test project, compiling cleanly, with `Oracle.ManagedDataAccess.Core` referenced.

- [ ] **Step 2.1 — Create solution and projects** (run from `GISWindowsApp/`)
  ```
  dotnet new sln -n GridTrace
  dotnet new wpf -n GridTrace -o src/GridTrace
  dotnet new xunit -n GridTrace.Tests -o tests/GridTrace.Tests
  ```

- [ ] **Step 2.2 — Add projects to solution**
  ```
  dotnet sln GridTrace.sln add src/GridTrace/GridTrace.csproj
  dotnet sln GridTrace.sln add tests/GridTrace.Tests/GridTrace.Tests.csproj
  ```

- [ ] **Step 2.3 — Add project reference from tests to app**
  ```
  dotnet add tests/GridTrace.Tests/GridTrace.Tests.csproj reference src/GridTrace/GridTrace.csproj
  ```

- [ ] **Step 2.4 — Add Oracle package to both projects**
  ```
  dotnet add src/GridTrace/GridTrace.csproj package Oracle.ManagedDataAccess.Core --version 23.5.1
  dotnet add tests/GridTrace.Tests/GridTrace.Tests.csproj package Oracle.ManagedDataAccess.Core --version 23.5.1
  ```

- [ ] **Step 2.5 — Build to confirm no errors**
  ```
  dotnet build GridTrace.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 2.6 — Commit**
  ```
  git add GISWindowsApp/GridTrace.sln GISWindowsApp/src GISWindowsApp/tests
  git commit -m "feat(gridtrace): scaffold WPF solution with Oracle package"
  ```

---

## HOUR 2 — Data Layer (1:00–2:00)

### Task 3: NetworkDevice Model and RelayCommand (15 min)

**Goal:** Plain data model and a reusable `ICommand` implementation for MVVM button bindings later.

**Files:**
- Create: `src/GridTrace/Models/NetworkDevice.cs`
- Create: `src/GridTrace/Commands/RelayCommand.cs`

- [ ] **Step 3.1 — Create the model**
  ```csharp
  namespace GridTrace.Models;

  public class NetworkDevice
  {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public string DeviceType { get; set; } = string.Empty; // SUBSTATION, FEEDER, POLE, TRANSFORMER, METER
      public int? ParentId { get; set; }
      public double PosX { get; set; }
      public double PosY { get; set; }
      public string Status { get; set; } = "NORMAL"; // NORMAL or OUTAGE
  }
  ```

- [ ] **Step 3.2 — Create RelayCommand**
  ```csharp
  using System;
  using System.Windows.Input;

  namespace GridTrace.Commands;

  public class RelayCommand : ICommand
  {
      private readonly Action<object?> _execute;
      private readonly Func<object?, bool>? _canExecute;

      public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
      {
          _execute = execute ?? throw new ArgumentNullException(nameof(execute));
          _canExecute = canExecute;
      }

      public event EventHandler? CanExecuteChanged
      {
          add => CommandManager.RequerySuggested += value;
          remove => CommandManager.RequerySuggested -= value;
      }

      public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

      public void Execute(object? parameter) => _execute(parameter);
  }
  ```

- [ ] **Step 3.3 — Build to confirm no errors**
  ```
  dotnet build GridTrace.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3.4 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/Models GISWindowsApp/src/GridTrace/Commands
  git commit -m "feat(gridtrace): add NetworkDevice model and RelayCommand"
  ```

---

### Task 4: DeviceRepository — Oracle Data Access (30 min, TDD)

**Goal:** ADO.NET repository wrapping `network_devices`, with the hierarchical trace query verified against the real seeded Oracle data.

**Files:**
- Create: `tests/GridTrace.Tests/TraceQueryTests.cs`
- Create: `src/GridTrace/Data/DeviceRepository.cs`

**Interfaces (produced for later tasks):**
- `DeviceRepository.GetAllDevicesAsync()` → `Task<List<NetworkDevice>>`
- `DeviceRepository.GetDownstreamTraceIdsAsync(int deviceId)` → `Task<List<int>>`
- `DeviceRepository.SetStatusAsync(IEnumerable<int> deviceIds, string status)` → `Task`
- `DeviceRepository.ResetAllStatusAsync()` → `Task`

- [ ] **Step 4.1 — Write the failing integration test**
  ```csharp
  using System.Linq;
  using System.Threading.Tasks;
  using GridTrace.Data;
  using Xunit;

  namespace GridTrace.Tests;

  public class TraceQueryTests
  {
      [Fact]
      public async Task GetDownstreamTraceIdsAsync_Feeder1_ReturnsAllDescendants()
      {
          var repository = new DeviceRepository();

          var ids = await repository.GetDownstreamTraceIdsAsync(2); // Feeder 1

          var expected = new[] { 2, 4, 5, 6, 10, 11, 12, 16, 17, 18, 19, 20 };
          Assert.Equal(expected.OrderBy(x => x), ids.OrderBy(x => x));
      }

      [Fact]
      public async Task GetDownstreamTraceIdsAsync_LeafMeter_ReturnsOnlyItself()
      {
          var repository = new DeviceRepository();

          var ids = await repository.GetDownstreamTraceIdsAsync(16); // Meter-1001, a leaf

          Assert.Equal(new[] { 16 }, ids);
      }
  }
  ```

- [ ] **Step 4.2 — Run test to verify it fails**
  ```
  dotnet test tests/GridTrace.Tests/GridTrace.Tests.csproj --filter TraceQueryTests
  ```
  Expected: build error — `The type or namespace name 'DeviceRepository' could not be found`

- [ ] **Step 4.3 — Implement DeviceRepository**
  ```csharp
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  using GridTrace.Models;
  using Oracle.ManagedDataAccess.Client;

  namespace GridTrace.Data;

  public class DeviceRepository
  {
      private const string ConnectionString =
          "User Id=system;Password=OraPassword1;Data Source=localhost:1521/XEPDB1;";

      public async Task<List<NetworkDevice>> GetAllDevicesAsync()
      {
          var devices = new List<NetworkDevice>();
          using var connection = new OracleConnection(ConnectionString);
          await connection.OpenAsync();
          using var command = new OracleCommand(
              "SELECT id, name, device_type, parent_id, pos_x, pos_y, status FROM network_devices ORDER BY id",
              connection);
          using var reader = await command.ExecuteReaderAsync();
          while (await reader.ReadAsync())
          {
              devices.Add(new NetworkDevice
              {
                  Id = reader.GetInt32(0),
                  Name = reader.GetString(1),
                  DeviceType = reader.GetString(2),
                  ParentId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                  PosX = reader.GetDouble(4),
                  PosY = reader.GetDouble(5),
                  Status = reader.GetString(6)
              });
          }
          return devices;
      }

      public async Task<List<int>> GetDownstreamTraceIdsAsync(int deviceId)
      {
          var ids = new List<int>();
          using var connection = new OracleConnection(ConnectionString);
          await connection.OpenAsync();
          using var command = new OracleCommand(
              "SELECT id FROM network_devices START WITH id = :deviceId CONNECT BY PRIOR id = parent_id",
              connection);
          command.Parameters.Add(new OracleParameter("deviceId", deviceId));
          using var reader = await command.ExecuteReaderAsync();
          while (await reader.ReadAsync())
          {
              ids.Add(reader.GetInt32(0));
          }
          return ids;
      }

      public async Task SetStatusAsync(IEnumerable<int> deviceIds, string status)
      {
          var idList = deviceIds.ToList();
          if (idList.Count == 0) return;

          using var connection = new OracleConnection(ConnectionString);
          await connection.OpenAsync();
          var inClause = string.Join(",", idList.Select((_, i) => $":id{i}"));
          using var command = new OracleCommand(
              $"UPDATE network_devices SET status = :status WHERE id IN ({inClause})", connection);
          command.Parameters.Add(new OracleParameter("status", status));
          for (int i = 0; i < idList.Count; i++)
          {
              command.Parameters.Add(new OracleParameter($"id{i}", idList[i]));
          }
          await command.ExecuteNonQueryAsync();
      }

      public async Task ResetAllStatusAsync()
      {
          using var connection = new OracleConnection(ConnectionString);
          await connection.OpenAsync();
          using var command = new OracleCommand(
              "UPDATE network_devices SET status = 'NORMAL'", connection);
          await command.ExecuteNonQueryAsync();
      }
  }
  ```

- [ ] **Step 4.4 — Run test to verify it passes**
  ```
  dotnet test tests/GridTrace.Tests/GridTrace.Tests.csproj --filter TraceQueryTests
  ```
  Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 4.5 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/Data GISWindowsApp/tests/GridTrace.Tests/TraceQueryTests.cs
  git commit -m "feat(gridtrace): add DeviceRepository with Oracle CONNECT BY PRIOR trace query"
  ```

---

## HOUR 3 — ViewModels (2:00–3:00)

### Task 5: DeviceViewModel (20 min)

**Goal:** Wraps `NetworkDevice` for data binding — exposes a `Status`-driven `Brush` for canvas coloring and raises `PropertyChanged` so the UI updates automatically when status changes.

**Files:**
- Create: `src/GridTrace/ViewModels/DeviceViewModel.cs`

**Interfaces (produced for later tasks):**
- `DeviceViewModel(NetworkDevice device)` constructor
- Properties: `Id`, `Name`, `DeviceType`, `ParentId`, `PosX`, `PosY`, `Status` (settable), `Fill` (Brush), `DisplayLabel` (string)

- [ ] **Step 5.1 — Create the ViewModel**
  ```csharp
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
  ```

- [ ] **Step 5.2 — Build to confirm no errors**
  ```
  dotnet build GridTrace.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5.3 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/ViewModels/DeviceViewModel.cs
  git commit -m "feat(gridtrace): add DeviceViewModel with status-driven brush"
  ```

---

### Task 6: MainViewModel (40 min)

**Goal:** Loads devices, exposes `SimulateOutageCommand` / `RestorePowerCommand`, and drives the status bar message. This is UI-orchestration logic — per the spec, testing is scoped to the trace query only (Task 4), so this task is build-verified, not unit-tested; you'll exercise it live in Hour 5.

**Files:**
- Create: `src/GridTrace/ViewModels/MainViewModel.cs`

**Interfaces (consumes):**
- `DeviceRepository.GetAllDevicesAsync/GetDownstreamTraceIdsAsync/SetStatusAsync/ResetAllStatusAsync` (Task 4)
- `DeviceViewModel(NetworkDevice)`, `.Id`, `.Status`, `.DeviceType` (Task 5)
- `RelayCommand` (Task 3)

**Interfaces (produced for Hour 4):**
- `MainViewModel.Devices` → `ObservableCollection<DeviceViewModel>`
- `MainViewModel.SelectedDevice` → `DeviceViewModel?` (settable)
- `MainViewModel.StatusMessage` → `string`
- `MainViewModel.SimulateOutageCommand`, `MainViewModel.RestorePowerCommand` → `ICommand`
- `MainViewModel.LoadDevicesAsync()` → `Task`

- [ ] **Step 6.1 — Create the ViewModel**
  ```csharp
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
  ```

  > **Note on `async _ => await ...` in RelayCommand:** this is an async-void command handler — the standard (if imperfect) WPF pattern for async commands without pulling in an extra library. Fine for this scope; exceptions inside are caught within each method so nothing crashes silently. Error handling is included directly here (folded in from what would otherwise be a separate "harden error handling" pass) since it costs nothing to write correctly the first time.

- [ ] **Step 6.2 — Build to confirm no errors**
  ```
  dotnet build GridTrace.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6.3 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/ViewModels/MainViewModel.cs
  git commit -m "feat(gridtrace): add MainViewModel with outage simulation and restore commands"
  ```

---

## HOUR 4 — XAML UI & Schematic Rendering (3:00–4:00)

### Task 7: MainWindow.xaml Layout (30 min)

**Goal:** Sidebar device list + action buttons + canvas area + status bar, all data-bound to `MainViewModel`.

**Files:**
- Modify: `src/GridTrace/MainWindow.xaml`
- Modify: `src/GridTrace/MainWindow.xaml.cs`

**Interfaces (consumes):** `MainViewModel.Devices`, `.SelectedDevice`, `.StatusMessage`, `.SimulateOutageCommand`, `.RestorePowerCommand`, `.LoadDevicesAsync()` (Task 6); `DeviceViewModel.DisplayLabel` (Task 5)

- [ ] **Step 7.1 — Replace MainWindow.xaml contents**
  ```xml
  <Window x:Class="GridTrace.MainWindow"
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          Title="GridTrace — Outage Impact Analyzer" Height="700" Width="1000">
      <DockPanel>
          <StatusBar DockPanel.Dock="Bottom">
              <StatusBarItem>
                  <TextBlock Text="{Binding StatusMessage}" />
              </StatusBarItem>
          </StatusBar>
          <Grid>
              <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="250" />
                  <ColumnDefinition Width="*" />
              </Grid.ColumnDefinitions>

              <DockPanel Grid.Column="0" Margin="8">
                  <StackPanel DockPanel.Dock="Bottom" Orientation="Vertical" Margin="0,8,0,0">
                      <Button Content="Simulate Outage" Command="{Binding SimulateOutageCommand}" Margin="0,0,0,4" Padding="4" />
                      <Button Content="Restore Power" Command="{Binding RestorePowerCommand}" Padding="4" />
                  </StackPanel>
                  <ListBox ItemsSource="{Binding Devices}"
                           SelectedItem="{Binding SelectedDevice}"
                           DisplayMemberPath="DisplayLabel" />
              </DockPanel>

              <Border Grid.Column="1" BorderBrush="Gray" BorderThickness="1" Margin="8">
                  <Canvas x:Name="SchematicCanvas" Background="White" Width="900" Height="520" />
              </Border>
          </Grid>
      </DockPanel>
  </Window>
  ```

- [ ] **Step 7.2 — Replace MainWindow.xaml.cs contents**
  ```csharp
  using System.ComponentModel;
  using System.Windows;
  using GridTrace.Rendering;
  using GridTrace.ViewModels;

  namespace GridTrace;

  public partial class MainWindow : Window
  {
      private readonly MainViewModel _viewModel = new();

      public MainWindow()
      {
          InitializeComponent();
          DataContext = _viewModel;
          Loaded += MainWindow_Loaded;
      }

      private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
      {
          await _viewModel.LoadDevicesAsync();

          foreach (var device in _viewModel.Devices)
          {
              device.PropertyChanged += Device_PropertyChanged;
          }

          RedrawCanvas();
      }

      private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
      {
          if (e.PropertyName == nameof(DeviceViewModel.Fill))
          {
              RedrawCanvas();
          }
      }

      private void RedrawCanvas()
      {
          SchematicRenderer.Render(SchematicCanvas, _viewModel.Devices);
      }
  }
  ```
  > This won't build yet — `SchematicRenderer` is created in Task 8. That's expected.

- [ ] **Step 7.3 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/MainWindow.xaml GISWindowsApp/src/GridTrace/MainWindow.xaml.cs
  git commit -m "feat(gridtrace): add MainWindow layout bound to MainViewModel"
  ```

---

### Task 8: SchematicRenderer (30 min)

**Goal:** Draws parent→child lines and device shapes onto the `Canvas`, color-coded by type/status. Called once after initial load and again any time a device's `Fill` changes (outage/restore).

**Files:**
- Create: `src/GridTrace/Rendering/SchematicRenderer.cs`

**Interfaces (consumes):** `DeviceViewModel.Id/ParentId/PosX/PosY/DeviceType/Fill/DisplayLabel` (Task 5)

- [ ] **Step 8.1 — Create the renderer**
  ```csharp
  using System.Collections.Generic;
  using System.Linq;
  using System.Windows.Controls;
  using System.Windows.Media;
  using System.Windows.Shapes;
  using GridTrace.ViewModels;

  namespace GridTrace.Rendering;

  public static class SchematicRenderer
  {
      public static void Render(Canvas canvas, IEnumerable<DeviceViewModel> devices)
      {
          canvas.Children.Clear();

          var deviceList = devices.ToList();
          var lookup = deviceList.ToDictionary(d => d.Id);

          foreach (var device in deviceList)
          {
              if (device.ParentId.HasValue && lookup.TryGetValue(device.ParentId.Value, out var parent))
              {
                  var line = new Line
                  {
                      X1 = parent.PosX,
                      Y1 = parent.PosY,
                      X2 = device.PosX,
                      Y2 = device.PosY,
                      Stroke = Brushes.Gray,
                      StrokeThickness = 1.5
                  };
                  canvas.Children.Add(line);
              }
          }

          foreach (var device in deviceList)
          {
              double size = device.DeviceType switch
              {
                  "SUBSTATION" => 36,
                  "FEEDER" => 24,
                  "POLE" => 16,
                  "TRANSFORMER" => 20,
                  "METER" => 12,
                  _ => 14
              };

              Shape shape = device.DeviceType == "SUBSTATION"
                  ? new Rectangle { Width = size, Height = size * 0.75 }
                  : new Ellipse { Width = size, Height = size };

              shape.Fill = device.Fill;
              shape.Stroke = Brushes.Black;
              shape.StrokeThickness = 1;
              shape.ToolTip = device.DisplayLabel;

              Canvas.SetLeft(shape, device.PosX - shape.Width / 2);
              Canvas.SetTop(shape, device.PosY - shape.Height / 2);
              canvas.Children.Add(shape);
          }
      }
  }
  ```

- [ ] **Step 8.2 — Build to confirm no errors**
  ```
  dotnet build GridTrace.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8.3 — Commit**
  ```
  git add GISWindowsApp/src/GridTrace/Rendering/SchematicRenderer.cs
  git commit -m "feat(gridtrace): add SchematicRenderer for canvas drawing"
  ```

---

## HOUR 5 — Verification, README, Wrap-up (4:00–5:00)

### Task 9: End-to-End Manual Verification (30 min)

**Goal:** Confirm the whole trace flow actually works against real Oracle data.

- [ ] **Step 9.1 — Run the app**
  ```
  dotnet run --project src/GridTrace/GridTrace.csproj
  ```

- [ ] **Step 9.2 — Verify initial load**
  All 25 devices appear in the sidebar list; the canvas shows the full tree (substation top-center, lines fanning down to meters at the bottom), no red shapes.

- [ ] **Step 9.3 — Verify a mid-tree trace**
  Select **"Feeder 1 (FEEDER)"**, click **Simulate Outage**. Expected: Feeder 1 + Poles 101/102/103 + XFMR-101/102/103 + Meters 1001/1002/1011/1021/1022 turn red (12 shapes). Status bar reads: `"12 devices affected, 5 customer meters without power."`

- [ ] **Step 9.4 — Verify restore**
  Click **Restore Power**. Expected: all shapes return to normal colors; status bar reads `"Power restored across all devices."`

- [ ] **Step 9.5 — Verify a leaf trace**
  Select **"Meter-1001 (METER)"**, click **Simulate Outage**. Expected: only that one shape turns red. Status bar reads `"1 devices affected, 1 customer meters without power."` Click **Restore Power** again afterward.

- [ ] **Step 9.6 — Verify connection-failure handling**
  ```
  docker stop oracle-spatial
  ```
  Restart the app (`dotnet run ...`). Expected: status bar shows the "Cannot connect to Oracle..." message instead of a crash. Then bring the container back:
  ```
  docker start oracle-spatial
  ```

---

### Task 10: README and Final Commit (15 min)

**Files:**
- Create: `GISWindowsApp/README.md`

- [ ] **Step 10.1 — Write the README**
  ```markdown
  # GridTrace

  WPF desktop app simulating an Oncor-style electric distribution network with
  outage impact tracing, inspired by Hexagon Intergraph G/Technology's
  connectivity trace capability. No Hexagon Intergraph API integration is
  used — this recreates the concept using Oracle's native hierarchical
  queries instead.

  ## Prerequisites
  - .NET 8 SDK
  - Docker container `oracle-spatial` running (Oracle XE 21c, port 1521, service XEPDB1)
  - `network_devices` table created and seeded — see Task 1 in
    `docs/superpowers/plans/2026-07-01-gridtrace.md`

  ## Running
  ```
  dotnet run --project src/GridTrace/GridTrace.csproj
  ```

  ## Running tests
  ```
  dotnet test tests/GridTrace.Tests/GridTrace.Tests.csproj
  ```

  ## Architecture
  - WPF + MVVM, .NET 8
  - Oracle.ManagedDataAccess.Core (raw ADO.NET, no EF Core)
  - Connectivity trace via Oracle's `START WITH ... CONNECT BY PRIOR` hierarchical query
  - Schematic Canvas rendering (no map tiles — see design spec for the tradeoff discussion)

  See `docs/superpowers/specs/2026-07-01-gridtrace-design.md` for full design rationale.
  ```

- [ ] **Step 10.2 — Final commit**
  ```
  git add GISWindowsApp/README.md
  git commit -m "docs(gridtrace): add project README"
  ```
