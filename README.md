# Ducktimizer

A local Windows 10 and 11 optimizer built by patoLizo with C# and Avalonia.
Free and open source under the MIT License. No account, payment, activation,
device identifier or connection to an author's service is required.

## Features

- Presets and eight reviewable groups for visual effects, gaming, power and privacy.
- Search settings, background activity controls and application removal.
- Granular backups before changes and an attempt to create a restore point.
- Restoration of saved settings and local logs.

**Review each group before applying it.** Removing applications may be irreversible:
Registry and power-plan backups do not guarantee that removed packages can be
reinstalled. FPS gains are not guaranteed. Only use computers you are authorized to manage.

## Development

Requires Windows and the .NET 10 SDK:

```powershell
dotnet restore .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
dotnet run --project .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
```

The application requests administrator privileges to change system settings.
The self-test checks the settings catalog and backup serialization without applying changes:

```powershell
dotnet run --project .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj -- --self-test
```

## Distribution

```powershell
.\build-release.ps1 -SkipInstaller  # standalone executable in outputs/publish
.\build-release.ps1                 # installer; requires Inno Setup 6
```

Install updates manually from the source or repository releases. There is no
automatic updater, commercial backend or telemetry. The former sales and
activation services are not part of this version.

## Local data

Logs and backups remain in the Ducktimizer folder within the user's local
application data. Uninstalling the application preserves backups. The open
version neither reads nor requires activation files from older versions.

## License and credits

The project's code uses the [MIT License](LICENSE), which also includes credits for the third-party Windows configuration mappings.
