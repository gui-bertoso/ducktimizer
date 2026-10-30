# Ducktimizer

[Download the Windows x64 preview](https://github.com/gui-bertoso/ducktimizer/releases/tag/v5.0.0-preview.2). Read the release limitations before applying changes.

A portable Windows preparation tool for a gaming-focused PC, written in C# and Avalonia. Analyze the machine, review the exact changes, export a baseline, apply once and inspect the results.

**Local development rewrite. Windows execution and performance gains have not been validated yet.** No FPS or service-count improvement is guaranteed. Compare a repeatable game benchmark before and after changes on a disposable Windows VM first.

## Current implementation

The source catalog contains 122 defined actions, plus recognized startup registrations discovered on the machine. The JSON contains more than 2,200 lines of definitions and usage constraints. Its Windows behavior has not yet been validated:

- Edge startup boost, background mode, new-tab content and shopping-assistant policies, with installation/version checks.
- Delivery Optimization peer sharing, preserving update downloads.
- Windows game recording policy, preserving Xbox and Gaming Services.
- Transparency and advertising-ID settings.
- Widgets policy, optional diagnostic-data policy and consumer-promotion policy, respecting supported editions.
- Diagnostic tracking service, with a guard for active/onboarded Defender for Endpoint.
- Exact CEIP Consolidator and UsbCeip scheduled tasks, only when present.
- Print Spooler and Fax services, when present and without running dependents.
- 33 exact removable app targets, including Copilot, Clipchamp, News, Weather, Solitaire, Teams, Phone Link and optional productivity/media apps.
- Offline maps and retail-demo services; media sharing, link tracking and Windows indexing are available but unchecked by default.
- Work Folders, XPS printing and Recall optional features, only when present and eligible.
- Recognized Run-key startup registrations for launchers and background apps; optional AMD/NVIDIA registrations are unchecked by default. Unknown registrations remain inventory-only; OneDrive startup removal is unchecked by default.

Additional settings cover diagnostic-data personalization, third-party Spotlight suggestions, Settings suggestions, welcome content and tips with edition checks. Current-user Explorer/provider and onboarding preferences are kept separate from supported policies; their visible behavior needs validation on the target build.

Five visual-effect actions use `SystemParametersInfoW` to inspect and update client-area, menu, tooltip, combo-box and minimize/restore animations individually. They read the native setting again during verification; font smoothing and focus indicators are not targeted.

Fourteen optional background-app actions resolve installed package families, including Teams, Phone Link, Outlook, To Do, recording/camera apps and other optional Windows apps. They merge that family into the computer-wide deny list, preserving other entries and the default policy. Existing per-app allow/user-control conflicts and unexpected registry types stop that action. Notifications and synchronization may be lost. These policies apply on supported Pro/Enterprise/Education editions and do not control all desktop-program helpers. A configured policy is not evidence that an app process stopped or that higher-priority management policy accepted it. Removed packages are skipped.

An unchecked power action selects the existing High performance scheme only if external power is confirmed and Battery Saver is off. The action does not create schemes or change CPU/thermal limits. It changes the power scheme rather than the separate Settings power-mode overlay. The plan remains selected after unplugging; no resident agent switches it back.

Package removal affects all users and removes provisioning for new users. Printing includes features such as Print to PDF. There is no restore function; application data may be lost and reinstalling removed apps is outside the tool's scope. Windows can reintroduce components in later updates.

Configured registry values are reported as configured, not as proof that a Windows policy took effect or that performance improved. Unsupported Windows editions are skipped for edition-limited policies. Outlook and media-player removals are unchecked by default. OEM programs, startup folders, unknown capabilities and unknown services/tasks remain inventory-only. Five exact optional capabilities can be removed when installed, explicitly selected and compatible with the chosen uses. Extra optional feature targets include IIS, SMB1, Telnet, TFTP, Simple TCP services and legacy PowerShell 2.0; running IIS services block IIS removal. Inventory findings are not evidence that a component is unnecessary.

## Run and develop

Requires the .NET 10 SDK to build. Windows 11 is required for analysis and execution. Linux opens a read-only catalog preview; it never runs Windows changes.

```sh
dotnet run --project Ducktimizer.csproj
dotnet run --project Ducktimizer.csproj -- --self-test
```

The app starts without administrator privileges. Use **Administrator access** for full package inspection and execution. Elevation must use the same Windows account so current-user settings cannot silently target another account.

1. Select every intended use of the PC, then analyze it. The multi-select questionnaire covers games, art, music/audio, streaming/video, work/study, development, printing/scanning, remote support, LAN files/media devices and general use.
2. Review the changes and consequences. Expand an item to include it or keep it unchanged. Changing the selection clears the acknowledgement. Blocked, unknown and unsupported items are excluded.
3. Analysis saves a local JSON baseline automatically. Export a copy from System details and record your game benchmark before proceeding.
4. Acknowledge the changes and apply. The app rechecks each reviewed action, records its start to disk and verifies its result.
5. Restart Windows if indicated, then repeat the benchmark under the same conditions.

Selected uses are stored in the baseline and run report. Actions whose `preserveFor` list intersects the selected uses are kept and labeled with the reason; the executor independently refuses to apply them. These are explicit compatibility constraints, not optimization presets or a complete dependency model. Core networking, audio, security and gaming dependencies remain protected separately. Changing the intended uses invalidates the analysis and acknowledgement. Choose all applicable uses and review remaining consequences.

The analyzer also collects three spaced CPU/disk-counter samples. Missing counters remain unavailable and are recorded as measurement warnings, never as zero. These samples include collection overhead and are not game FPS or a substitute for repeated idle/game benchmarks.

The results screen lists the final status of each attempted action and can export the complete event record as JSON. Failed reinspection stops the run before further changes; an already-configured item is skipped.

Optional-feature and capability operations return Windows servicing restart requirements to the executor. Execution pauses when Windows reports a servicing restart or a pending feature restart; restart and analyze again to continue. No automatic reboot is performed. Execution stops on a failure. Stop requests take effect between actions. Re-running skips configured or absent targets; it does not blindly repeat the entire operation.

## Portable build

```powershell
.\build-release.ps1
```

Produces a self-contained single-file `outputs/portable/win-x64/Ducktimizer.exe` and a portable ZIP under `outputs/releases`, containing the executable, project license, risk notice and dependency license notices. A SHA-256 sidecar is generated for the ZIP. There is no installer, background agent or update service. .NET may extract native libraries into its temporary runtime cache; the executable does not register an installed application.

Logs and per-run JSON records are written under `%LOCALAPPDATA%\Ducktimizer`. Records are audit information, not a rollback mechanism. Exported baselines include OS details, services and dependencies, scheduled tasks, startup-entry names, app packages, optional features, capabilities, installed desktop-program metadata, disk capacity, GPU/driver metadata, active power scheme/source and process-memory summaries with process IDs and associated service names. Shared service-host memory is not attributed to one service, and working-set totals must not be summed as exclusive physical memory. Command lines, uninstall commands and user identifiers are not deliberately collected; names and error messages may still contain personal information. Review exports before sharing. Inventory-read failures are recorded as warnings.

The preview release is built from this source. Simulated execution checks and Linux interface checks do not validate Windows mutation behavior or game performance. Windows execution and before/after benchmarks remain pending.

## Editing the optimization table

`Catalog/optimizations.json` is the single definition table for static changes and recognized startup registrations. The app loads it at startup, validates it, then uses those same definitions for analysis, review and execution. It is embedded when building so the portable executable still consists of one file. Editing the source JSON requires rebuilding the executable; there is no sidecar override or remote download.

Each action has a `preserveFor` list of machine-use IDs, a stable `id`, a `kind`, an exact `target`, a description, its consequences and compatibility requirements. Registry actions also specify `valueName` and `desiredValue`. `defaultIncluded` controls the initial review selection; it does not bypass analysis or acknowledgement. The `startup` section lists exact registration names, usage protections and allowed Run-key locations. Keep existing IDs stable so old audit records remain understandable.

The table selects typed operations rather than arbitrary command strings. `Platform/WindowsOperations.ps1` contains the shared inspection and execution handlers for registry values, services, app packages, tasks, startup entries, optional features per-package background policies and exact Windows capability removal. `Platform/WindowsNativeSettings.cs` handles the restricted visual-effect and power-scheme operations through native Windows APIs. `Platform/WindowsInventory.ps1` reads inventory. Adding another target for a supported operation belongs in the JSON; adding a new operation type requires implementing its handler. Protected services, exact task/feature allowlists and dependency checks remain enforced in code and may require an explicit source change when extending those targets.

Use four spaces for indentation, as defined by `.editorconfig`. Generated `bin`, `obj` and publishing output are ignored and can be recreated; keep them out of source changes.

## Source layout

The application project lives at the repository root. `Views` and `ViewModels` contain the interface, `Catalog/optimizations.json` defines changes and startup rules, `Catalog` loads and validates them, `Platform` accesses Windows, `Execution` records and verifies runs, and `Models` holds shared data. `tests/SelfTests.cs` exercises the execution engine with simulated platform operations and makes no system changes.

## Terms and risk notice

Read [TERMS.txt](TERMS.txt) before applying changes. The interface displays the same embedded notice before its acknowledgement checkbox. Each UI-initiated run records the notice version, SHA-256 digest and acknowledgement timestamp locally. This is an application event record, not identity verification or a guarantee of legal enforceability. Simulated/internal execution can omit acknowledgement and is recorded without one; no acceptance is invented for those runs.

The notice supplements risk information without adding restrictions to the MIT License or waiving non-waivable legal rights. It has not been reviewed by a lawyer and does not guarantee protection against legal claims. The notice is included in the preview build; legal enforceability has not been reviewed by a lawyer.

## License

[MIT](LICENSE), including the retained upstream credits. Dependencies retain their own licenses.
