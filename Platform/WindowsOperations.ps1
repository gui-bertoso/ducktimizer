function Get-BackgroundPolicyList([string]$name) {
    $path = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy'
    if (-not (Test-Path -LiteralPath $path)) {
        return
    }

    $key = Get-Item -LiteralPath $path
    if ($key.GetValueNames() -notcontains $name) {
        return
    }

    if ($key.GetValueKind($name).ToString() -ne 'MultiString') {
        throw ('The existing background policy has an unexpected value type: ' + $name)
    }

    foreach ($family in @($key.GetValue($name))) {
        if ([string]::IsNullOrWhiteSpace($family)) {
            continue
        }
        if ($family -notmatch '^[A-Za-z0-9.-]+_[a-z0-9]{13}$') {
            throw ('The existing background policy contains an unsupported package-family entry: ' + $name)
        }
        [string]$family
    }
}

function Get-BackgroundContext($a) {
    if ($request.Operation -eq 'analyze') {
        if (@($script:inventorySectionErrors | Where-Object { $_.StartsWith('App packages:') }).Count) {
            throw 'App-package inventory could not be read.'
        }
        $packages = @($script:inventoryInstalled | Where-Object Name -eq $a.Target)
    } else {
        $packages = @(Get-AppxPackage -AllUsers -Name $a.Target -ErrorAction Stop)
    }

    if (@($packages | Where-Object { $_.IsFramework -or $_.NonRemovable }).Count) {
        throw 'Framework and non-removable packages are not eligible for this background restriction.'
    }

    $families = @($packages | ForEach-Object PackageFamilyName | Sort-Object -Unique)
    if ($families.Count -gt 1) {
        throw 'Multiple publisher families were found for this package. No family will be selected automatically.'
    }

    $family = if ($families.Count) { [string]$families[0] } else { '' }
    if ($packages.Count -gt 0 -and $family -notmatch '^[A-Za-z0-9.-]+_[a-z0-9]{13}$') {
        throw 'Windows returned an invalid package-family identity.'
    }

    $default = $null
    $path = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy'
    if (Test-Path -LiteralPath $path) {
        $key = Get-Item -LiteralPath $path
        if ($key.GetValueNames() -contains 'LetAppsRunInBackground') {
            if ($key.GetValueKind('LetAppsRunInBackground').ToString() -ne 'DWord') {
                throw 'The existing background-app default has an unexpected registry type.'
            }
            $default = $key.GetValue('LetAppsRunInBackground')
            if ($default -notin @(0, 1, 2)) {
                throw 'The existing background-app default is not recognized.'
            }
        }
    }

    @{
        Family = $family
        Default = $default
        Denied = @(Get-BackgroundPolicyList 'LetAppsRunInBackground_ForceDenyTheseApps')
        Allowed = @(Get-BackgroundPolicyList 'LetAppsRunInBackground_ForceAllowTheseApps')
        UserControlled = @(Get-BackgroundPolicyList 'LetAppsRunInBackground_UserInControlOfTheseApps')
    }
}

function Get-BackgroundObservation($a) {
    $context = Get-BackgroundContext $a
    if (-not $context.Family) {
        return @{
            Id = $a.Id
            State = 'NotApplicable'
            Detail = 'No installed package family was found. Provisioning alone is not targeted.'
        }
    }

    if ($context.Allowed -contains $context.Family -or $context.UserControlled -contains $context.Family) {
        return @{
            Id = $a.Id
            State = 'Blocked'
            Detail = 'An existing per-app allow or user-control policy conflicts with this restriction. It will not be overwritten.'
        }
    }

    if ($null -ne $context.Default -and ($context.Default -eq 2 -or $context.Denied -contains $context.Family)) {
        return @{
            Id = $a.Id
            State = 'Configured'
            Detail = ('A background deny policy is configured for ' + $context.Family + '. This verifies policy configuration, not process termination or effective policy precedence.')
        }
    }

    return @{
        Id = $a.Id
        State = 'Pending'
        Detail = ('Installed family: ' + $context.Family + '. The existing default and other per-app entries will be preserved. Reopen the app or restart Windows for policy application.')
    }
}

function Set-BackgroundRestriction($a) {
    $context = Get-BackgroundContext $a
    if (-not $context.Family -or $context.Allowed -contains $context.Family -or $context.UserControlled -contains $context.Family) {
        throw 'The package or background-policy eligibility changed before application.'
    }

    $path = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy'
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -Path $path -Force | Out-Null
    }

    if ($null -eq $context.Default) {
        # Enable the policy with its user-controlled default, rather than deny every app.
        New-ItemProperty -LiteralPath $path -Name 'LetAppsRunInBackground' -Value 0 -PropertyType DWord -Force | Out-Null
    }

    $families = @(@($context.Denied) + @($context.Family) | Sort-Object -Unique)
    New-ItemProperty -LiteralPath $path -Name 'LetAppsRunInBackground_ForceDenyTheseApps' -Value ([string[]]$families) -PropertyType MultiString -Force | Out-Null
}

function Get-Edition {
    (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name EditionID).EditionID
}

function Get-ExactTask($a) {
    if ($request.Operation -eq 'analyze') {
        if (@($script:inventorySectionErrors | Where-Object {
            $_.StartsWith('Scheduled tasks:')
        }).Count) {
            throw 'Scheduled-task inventory could not be read.'
        }
        return @($script:inventoryTasks | Where-Object {
            ($_.TaskPath + $_.TaskName) -eq $a.Target
        })
    }
    @(Get-ScheduledTask -ErrorAction Stop | Where-Object {
        ($_.TaskPath + $_.TaskName) -eq $a.Target
    })
}

function Get-Observation($a) {
    try {
        $build = [Environment]::OSVersion.Version.Build
        if ($build -lt [Math]::Max(22000, [int]$a.MinimumBuild)) {
            return @{
                Id = $a.Id
                State = 'NotApplicable'
                Detail = 'This development catalog targets Windows 11.'
            }
        }
        if ($a.RequiresPro -and (Get-Edition) -notmatch '^(Professional|Enterprise|Education|IoTEnterprise)') {
            return @{
                Id = $a.Id
                State = 'NotApplicable'
                Detail = 'This policy is not enabled for the detected Windows edition in this catalog.'
            }
        }
        if ($a.RequiresEnterprise -and (Get-Edition) -notmatch '^(Enterprise|Education|IoTEnterprise)') {
            return @{
                Id = $a.Id
                State = 'NotApplicable'
                Detail = 'This policy requires an Enterprise or Education edition in this catalog.'
            }
        }
        if ($a.Kind -eq 'Registry' -and $a.Target -eq 'HKLM:\SOFTWARE\Policies\Microsoft\Edge') {
            $version = Get-EdgeVersion
            if (-not $version) {
                return @{
                    Id = $a.Id
                    State = 'NotApplicable'
                    Detail = 'A standard Microsoft Edge browser installation was not found. WebView2 alone is not a browser installation.'
                }
            }
            if ($a.MinimumEdgeMajor -gt 0 -and [int]($version.Split('.')[0]) -lt $a.MinimumEdgeMajor) {
                return @{
                    Id = $a.Id
                    State = 'NotApplicable'
                    Detail = 'The detected Edge version is older than this policy supports.'
                }
            }
        }
        switch ($a.Kind) {
            'BackgroundApp' {
                return Get-BackgroundObservation $a
            }
            'Registry' {
                $current = $null
                $kind = $null
                if (Test-Path -LiteralPath $a.Target) {
                    $key = Get-Item -LiteralPath $a.Target
                    $current = $key.GetValue($a.ValueName, $null)
                    if ($null -ne $current) {
                        $kind = $key.GetValueKind($a.ValueName).ToString()
                    }
                }
                if ($null -ne $current -and $kind -eq 'DWord' -and [string]$current -eq [string]$a.DesiredValue) {
                    return @{
                        Id = $a.Id
                        State = 'Configured'
                        Detail = 'The requested value is configured. Effective behavior still needs validation after sign-out or restart.'
                    }
                }
                $label = if ($null -eq $current) {
                    'Not explicitly configured'
                } else {
                    [string]$current
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = "Current value: $label. Requested value: $($a.DesiredValue)."
                }
            }
            'StartupEntry' {
                if (-not (Test-Path -LiteralPath $a.Target)) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'This startup registration is absent.'
                    }
                }
                $key = Get-Item -LiteralPath $a.Target
                if ($key.GetValueNames() -notcontains $a.ValueName) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'This startup registration is absent.'
                    }
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = 'This exact Run-key registration exists. Task Manager may separately disable it; that flag is not interpreted. The program and its files will remain installed.'
                }
            }
            'Capability' {
                if ($request.Operation -eq 'analyze') {
                    if (@($script:inventorySectionErrors | Where-Object { $_.StartsWith('Windows capabilities:') }).Count) {
                        throw 'Windows capability inventory could not be read.'
                    }
                    $capability = @($script:inventoryCapabilities | Where-Object Name -eq $a.Target)
                } else {
                    $capability = @(Get-WindowsCapability -Online -ErrorAction Stop | Where-Object Name -eq $a.Target)
                }

                if ($capability.Count -eq 0) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'This exact capability is not available on this Windows image.'
                    }
                }

                $state = $capability[0].State.ToString()
                if ($state -in @('InstallPending', 'UninstallPending')) {
                    return @{
                        Id = $a.Id
                        State = 'RestartRequired'
                        Detail = ('Capability state: ' + $state + '. Restart before continuing.')
                    }
                }
                if ($state -eq 'NotPresent') {
                    return @{
                        Id = $a.Id
                        State = 'Configured'
                        Detail = 'This capability is not installed. No removal is needed.'
                    }
                }
                if ($state -ne 'Installed') {
                    return @{
                        Id = $a.Id
                        State = 'Blocked'
                        Detail = ('Unrecognized capability state: ' + $state)
                    }
                }

                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = 'This exact capability is installed. Its functionality will be removed; resource savings are unmeasured.'
                }
            }
            'OptionalFeature' {
                if ($request.Operation -eq 'analyze') {
                    if (@($script:inventorySectionErrors | Where-Object {
                        $_.StartsWith('Optional features:')
                    }).Count) {
                        throw 'Optional-feature inventory could not be read.'
                    }
                    $feature = @($script:inventoryFeatures | Where-Object Name -eq $a.Target)
                } else {
                    $feature = @(Get-WindowsOptionalFeature -Online -ErrorAction Stop | Where-Object FeatureName -eq $a.Target)
                }
                if ($feature.Count -eq 0) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'This optional feature is not present on this Windows image.'
                    }
                }
                if ($a.Target -eq 'IIS-WebServerRole') {
                    $activeIis = @(Get-CimInstance Win32_Service -Filter "(Name='W3SVC' OR Name='WAS') AND State='Running'" -ErrorAction Stop)
                    if ($activeIis.Count -gt 0) {
                        return @{
                            Id = $a.Id
                            State = 'Blocked'
                            Detail = 'IIS services are running. Active local sites will not be disabled by this action.'
                        }
                    }
                }
                $state = $feature[0].State.ToString()
                if ($state -in @('Disabled','DisabledWithPayloadRemoved')) {
                    return @{
                        Id = $a.Id
                        State = 'Configured'
                        Detail = ('Feature state: ' + $state + '. No change will be made.')
                    }
                }
                if ($state -in @('DisablePending','EnablePending')) {
                    return @{
                        Id = $a.Id
                        State = 'RestartRequired'
                        Detail = ('Feature state: ' + $state + '. Restart Windows before further changes.')
                    }
                }
                if ($state -ne 'Enabled') {
                    return @{
                        Id = $a.Id
                        State = 'Blocked'
                        Detail = ('Unrecognized feature state: ' + $state)
                    }
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = 'Feature is enabled. Shared files may remain after disabling and payload removal.'
                }
            }
            'ScheduledTask' {
                $tasks = @(Get-ExactTask $a)
                if ($tasks.Count -eq 0) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'This scheduled task is not installed.'
                    }
                }
                $task = $tasks[0]
                if (-not $task.Settings.Enabled -and $task.State -ne 'Running') {
                    return @{
                        Id = $a.Id
                        State = 'Configured'
                        Detail = 'The scheduled task is disabled and is not running.'
                    }
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = "$($a.Target): $($task.State); enabled: $($task.Settings.Enabled)."
                }
            }
            'Service' {
                if ($a.Target -eq 'DiagTrack') {
                    $sense = Get-CimInstance Win32_Service -Filter "Name='Sense'"
                    $onboarded = $false
                    $mdePath = 'HKLM:\SOFTWARE\Microsoft\Windows Advanced Threat Protection\Status'
                    if (Test-Path -LiteralPath $mdePath) {
                        $mde = Get-Item -LiteralPath $mdePath
                        $onboarded = $mde.GetValue('OnboardingState', 0) -eq 1
                    }
                    if ($onboarded -or ($sense -and $sense.State -eq 'Running')) {
                        return @{
                            Id = $a.Id
                            State = 'Blocked'
                            Detail = 'Defender for Endpoint is active or onboarded. Its diagnostic dependencies will not be disabled.'
                        }
                    }
                }
                $service = Get-CimInstance Win32_Service -Filter ("Name='" + $a.Target + "'")
                if ($null -eq $service) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'Service is not installed.'
                    }
                }
                if ($service.StartMode -eq 'Disabled' -and $service.State -eq 'Stopped') {
                    return @{
                        Id = $a.Id
                        State = 'Configured'
                        Detail = 'Service is stopped and its startup type is disabled.'
                    }
                }
                $dependents = @((Get-Service -Name $a.Target).DependentServices | Where-Object Status -eq Running)
                if ($dependents.Count -gt 0) {
                    return @{
                        Id = $a.Id
                        State = 'Blocked'
                        Detail = ('Running dependent services: ' + (($dependents | ForEach-Object Name) -join ', ') + '. They will not be stopped automatically.')
                    }
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = "$($service.State); startup: $($service.StartMode)."
                }
            }
            'Application' {
                if ($request.Operation -eq 'analyze') {
                    if (@($script:inventorySectionErrors | Where-Object {
                        $_.StartsWith('App packages:')
                    }).Count) {
                        throw 'App-package inventory could not be read.'
                    }
                    $installed = @($script:inventoryInstalled | Where-Object Name -eq $a.Target)
                    $provisioned = @($script:inventoryProvisioned | Where-Object DisplayName -eq $a.Target)
                } else {
                    $installed = @(Get-AppxPackage -AllUsers -Name $a.Target)
                    $provisioned = @(Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq $a.Target)
                }
                if (@($installed | Where-Object {
                    $_.NonRemovable -or $_.IsFramework
                }).Count -gt 0) {
                    return @{
                        Id = $a.Id
                        State = 'Blocked'
                        Detail = 'This is a framework or Windows marks it as non-removable.'
                    }
                }
                if ($installed.Count -eq 0 -and $provisioned.Count -eq 0) {
                    return @{
                        Id = $a.Id
                        State = 'NotApplicable'
                        Detail = 'No installed or provisioned package was found.'
                    }
                }
                return @{
                    Id = $a.Id
                    State = 'Pending'
                    Detail = "$($installed.Count) installed package(s); $($provisioned.Count) provisioned package(s). Removal affects all users."
                }
            }
            default {
                throw 'Unknown action kind.'
            }
        }
    } catch {
        return @{
            Id = $a.Id
            State = 'Failed'
            Detail = $_.Exception.Message
        }
    }
}

function Set-DesiredState($a) {
    switch ($a.Kind) {
        'BackgroundApp' {
            Set-BackgroundRestriction $a
        }
        'Registry' {
            if (-not (Test-Path -LiteralPath $a.Target)) {
                New-Item -Path $a.Target -Force | Out-Null
            }
            New-ItemProperty -LiteralPath $a.Target -Name $a.ValueName -Value ([int]$a.DesiredValue) -PropertyType DWord -Force | Out-Null
        }
        'StartupEntry' {
            Remove-ItemProperty -LiteralPath $a.Target -Name $a.ValueName -ErrorAction Stop
        }
        'OptionalFeature' {
            $result = Disable-WindowsOptionalFeature -Online -FeatureName $a.Target -Remove -NoRestart -ErrorAction Stop
            @{ RestartRequired = [bool]$result.RestartNeeded } | ConvertTo-Json -Compress
        }
        'Capability' {
            $result = Remove-WindowsCapability -Online -Name $a.Target -ErrorAction Stop
            @{ RestartRequired = [bool]$result.RestartNeeded } | ConvertTo-Json -Compress
        }
        'ScheduledTask' {
            $tasks = @(Get-ExactTask $a)
            foreach ($task in $tasks) {
                Disable-ScheduledTask -TaskPath $task.TaskPath -TaskName $task.TaskName -ErrorAction Stop | Out-Null
                if ($task.State -eq 'Running') {
                    Stop-ScheduledTask -TaskPath $task.TaskPath -TaskName $task.TaskName -ErrorAction Stop
                }
            }
        }
        'Service' {
            Stop-Service -Name $a.Target -ErrorAction Stop
            (Get-Service -Name $a.Target).WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
            Set-Service -Name $a.Target -StartupType Disabled -ErrorAction Stop
        }
        'Application' {
            foreach ($package in @(Get-AppxPackage -AllUsers -Name $a.Target)) {
                Remove-AppxPackage -Package $package.PackageFullName -AllUsers -ErrorAction Stop
            }
            foreach ($package in @(Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq $a.Target)) {
                Remove-AppxProvisionedPackage -Online -PackageName $package.PackageName -AllUsers -ErrorAction Stop | Out-Null
            }
        }
        default {
            throw 'Unknown action kind.'
        }
    }
}
if ($request.Operation -eq 'analyze') {
    $inventory = Get-SystemInventory
    $os = Get-CimInstance Win32_OperatingSystem
    $results = @($request.Actions | ForEach-Object {
        Get-Observation $_
    })
    $cpuSamples = @()
    $readSamples = @()
    $writeSamples = @()
    $warnings = @()
    for ($i = 0; $i -lt 3; $i++) {
        Start-Sleep -Seconds 1
        try {
            $cpu = Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'"
            if ($null -eq $cpu) {
                throw 'CPU performance counters are unavailable.'
            }
            $cpuSamples += [double]$cpu.PercentProcessorTime
        } catch {
            $warnings += $_.Exception.Message
        }
        try {
            $disk = Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk -Filter "Name='_Total'"
            if ($null -eq $disk) {
                throw 'Disk performance counters are unavailable.'
            }
            $readSamples += [double]$disk.DiskReadBytesPersec
            $writeSamples += [double]$disk.DiskWriteBytesPersec
        } catch {
            $warnings += $_.Exception.Message
        }
    }
    # Refresh memory after catalog inspection; resource samples include the analyzer's own overhead.
    $os = Get-CimInstance Win32_OperatingSystem
    @{
        AverageCpuPercent = if ($cpuSamples.Count) {
            ($cpuSamples | Measure-Object -Average).Average
        } else {
            $null
        }
        AverageDiskReadBytesPerSecond = if ($readSamples.Count) {
            ($readSamples | Measure-Object -Average).Average
        } else {
            $null
        }
        AverageDiskWriteBytesPerSecond = if ($writeSamples.Count) {
            ($writeSamples | Measure-Object -Average).Average
        } else {
            $null
        }
        ResourceSampleCount = $cpuSamples.Count
        MeasurementWarnings = @($warnings | Sort-Object -Unique)
        CapturedAt = [DateTimeOffset]::UtcNow.ToString('o')
        OperatingSystem = $os.Caption
        Build = [int]$os.BuildNumber
        Edition = (Get-Edition)
        RunningServices = @(Get-Service | Where-Object Status -eq Running).Count
        Processes = @(Get-Process).Count
        TotalMemoryMiB = [long]($os.TotalVisibleMemorySize / 1024)
        UsedMemoryMiB = [long](($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1024)
        Actions = $results
        Inventory = $inventory
    } | ConvertTo-Json -Depth 8 -Compress
} elseif ($request.Operation -eq 'inspect-many') {
    $results = @($request.Actions | ForEach-Object {
        Get-Observation $_
    })
    ConvertTo-Json -InputObject $results -Depth 5 -Compress
} elseif ($request.Operation -eq 'inspect') {
    Get-Observation $request.Actions[0] | ConvertTo-Json -Depth 5 -Compress
} elseif ($request.Operation -in @('apply', 'ensure-ready')) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Administrator access is required to apply changes.'
    }
    if (Test-RestartPending) {
        throw 'Windows has a pending restart. Restart before applying further changes.'
    }
    if ($request.Operation -eq 'apply') {
        $a = $request.Actions[0]
        $before = Get-Observation $a
        if ($before.State -ne 'Pending') {
            throw ('Action is no longer eligible: ' + $before.Detail)
        }
        Set-DesiredState $a
    }
} else {
    throw 'Unknown operation.'
}
