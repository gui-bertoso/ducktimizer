function Get-EdgeVersion {
    foreach ($root in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, $env:LOCALAPPDATA)) {
        if (-not $root) {
            continue
        }
        $path = Join-Path $root 'Microsoft\Edge\Application\msedge.exe'
        if (Test-Path -LiteralPath $path) {
            return (Get-Item -LiteralPath $path).VersionInfo.ProductVersion
        }
    }
    return ''
}

function Test-RestartPending {
    foreach ($key in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')) {
        if (Test-Path -LiteralPath $key) {
            return $true
        }
    }
    return $false
}

function Read-InventorySection([string]$name, [scriptblock]$read) {
    try {
        & $read
    }
    catch {
        $script:inventoryWarnings.Add($name + ': ' + $_.Exception.Message)
    }
}

function Get-SystemInventory {
    $script:inventoryWarnings = [Collections.Generic.List[string]]::new()
    $services = @(Read-InventorySection 'Services' {
        $controllers = @{}
        foreach ($service in @(Get-Service -ErrorAction Stop)) {
            $controllers[$service.Name] = $service
        }
        Get-CimInstance Win32_Service | ForEach-Object {
            @{
                Name = $_.Name
                DisplayName = $_.DisplayName
                State = $_.State
                StartMode = $_.StartMode
                ProcessId = [int]$_.ProcessId
                Dependents = @($controllers[$_.Name].DependentServices | ForEach-Object Name)
            }
        }
    })
    $tasks = @(Read-InventorySection 'Scheduled tasks' {
        $script:inventoryTasks = @(Get-ScheduledTask -ErrorAction Stop)
        $script:inventoryTasks | ForEach-Object {
            @{
                Path = $_.TaskPath
                Name = $_.TaskName
                State = $_.State.ToString()
                Enabled = [bool]$_.Settings.Enabled
            }
        }
    })
    $startup = @(Read-InventorySection 'Startup registrations' {
        foreach ($keyName in @($request.StartupRunKeys)) {
            if (Test-Path -LiteralPath $keyName) {
                $key = Get-Item -LiteralPath $keyName
                foreach ($name in $key.GetValueNames()) {
                    if ($name) {
                        @{
                            Name = $name
                            Location = $keyName
                            Scope = if ($keyName.StartsWith('HKCU:')) {
                                'Current user'
                            } else {
                                'All users'
                            }
                            Kind = 'Registry'
                        }
                    }
                }
            }
        }
    })
    $startupFolders = @(Read-InventorySection 'Startup folders' {
        foreach ($kind in @('Startup','CommonStartup')) {
            $folder = [Environment]::GetFolderPath($kind)
            if ($folder -and (Test-Path -LiteralPath $folder)) {
                Get-ChildItem -LiteralPath $folder -File | Where-Object Name -ne 'desktop.ini' | ForEach-Object {
                    @{
                        Name = $_.Name
                        Location = $kind
                        Scope = if ($kind -eq 'Startup') {
                            'Current user'
                        } else {
                            'All users'
                        }
                        Kind = 'StartupFolder'
                    }
                }
            }
        }
    })
    $packages = @(Read-InventorySection 'App packages' {
        $provisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction Stop)
        $installed = @(Get-AppxPackage -AllUsers -ErrorAction Stop)
        $script:inventoryProvisioned = $provisioned
        $script:inventoryInstalled = $installed
        $names = @($installed | ForEach-Object Name) + @($provisioned | ForEach-Object DisplayName)
        foreach ($name in @($names | Sort-Object -Unique)) {
            $matches = @($installed | Where-Object Name -eq $name)
            $version = if ($matches.Count) {
                $matches[0].Version.ToString()
            } else {
                ''
            }
            @{
                Name = $name
                Version = $version
                NonRemovable = [bool](@($matches | Where-Object NonRemovable -eq $true).Count)
                IsFramework = [bool](@($matches | Where-Object IsFramework -eq $true).Count)
                Provisioned = [bool](@($provisioned | Where-Object DisplayName -eq $name).Count)
            }
        }
    })
    $features = @(Read-InventorySection 'Optional features' {
        Get-WindowsOptionalFeature -Online -ErrorAction Stop | ForEach-Object {
            @{
                Name = $_.FeatureName
                State = $_.State.ToString()
            }
        }
    })
    $script:inventoryFeatures = $features
    $capabilities = @(Read-InventorySection 'Windows capabilities' {
        Get-WindowsCapability -Online -ErrorAction Stop | ForEach-Object {
            @{
                Name = $_.Name
                State = $_.State.ToString()
            }
        }
    })
    $script:inventoryCapabilities = $capabilities
    $programs = @(Read-InventorySection 'Installed desktop programs' {
        foreach ($root in @('HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
            if (Test-Path -LiteralPath $root) {
                Get-ChildItem -LiteralPath $root | ForEach-Object {
                    $value = Get-ItemProperty -LiteralPath $_.PSPath
                    if ($value.DisplayName) {
                        @{
                            Name = [string]$value.DisplayName
                            Publisher = [string]$value.Publisher
                            Version = [string]$value.DisplayVersion
                        }
                    }
                }
            }
        }
    })
    $disks = @(Read-InventorySection 'Fixed disks' {
        Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {
            @{
                Drive = $_.DeviceID
                SizeMiB = [long]($_.Size / 1MB)
                FreeMiB = [long]($_.FreeSpace / 1MB)
            }
        }
    })
    $processes = @(Read-InventorySection 'Process memory' {
        Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 12 | ForEach-Object {
            $process = $_
            @{
                Name = $process.ProcessName
                ProcessId = [int]$process.Id
                WorkingSetMiB = [long]($process.WorkingSet64 / 1MB)
                Services = @($services | Where-Object {
                    $_.ProcessId -gt 0 -and $_.ProcessId -eq $process.Id
                } | ForEach-Object Name)
            }
        }
    })
    $graphics = @(Read-InventorySection 'Graphics adapters' {
        Get-CimInstance Win32_VideoController -ErrorAction Stop | ForEach-Object {
            @{
                Name = [string]$_.Name
                DriverVersion = [string]$_.DriverVersion
                Manufacturer = [string]$_.AdapterCompatibility
            }
        }
    })
    $script:inventorySectionErrors = @($script:inventoryWarnings.ToArray())
    $boot = $null
    $edge = ''
    $restart = $false
    try {
        $boot = ([DateTimeOffset](Get-CimInstance Win32_OperatingSystem).LastBootUpTime).ToString('o')
    }
    catch {
        $script:inventoryWarnings.Add('Boot time: ' + $_.Exception.Message)
    }
    try {
        $edge = Get-EdgeVersion
    } catch {
        $script:inventoryWarnings.Add('Edge version: ' + $_.Exception.Message)
    }
    try {
        $restart = Test-RestartPending
    } catch {
        $script:inventoryWarnings.Add('Restart state: ' + $_.Exception.Message)
        $restart = $true
    }
    @{
        Services = $services
        ScheduledTasks = $tasks
        StartupEntries = @($startup + $startupFolders)
        Packages = $packages
        OptionalFeatures = $features
        Capabilities = $capabilities
        InstalledPrograms = $programs
        Disks = $disks
        LargestProcesses = $processes
        Graphics = $graphics
        LastBootTime = $boot
        EdgeVersion = $edge
        RestartPending = $restart
        Warnings = @($script:inventoryWarnings.ToArray())
    }
}
