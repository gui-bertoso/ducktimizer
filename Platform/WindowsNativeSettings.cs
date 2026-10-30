using System.ComponentModel;
using System.Runtime.InteropServices;
using Ducktimizer.Models;

namespace Ducktimizer.Platform;

internal static class WindowsNativeSettings
{
    internal const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    // Only these native operations are exposed to the optimization table.
    private static readonly IReadOnlyDictionary<string, (uint Read, uint Write)> Effects =
        new Dictionary<string, (uint, uint)>(StringComparer.Ordinal)
        {
            ["client-animation"] = (0x1042, 0x1043),
            ["menu-animation"] = (0x1002, 0x1003),
            ["tooltip-animation"] = (0x1016, 0x1017),
            ["combobox-animation"] = (0x1004, 0x1005),
            ["window-animation"] = (0x0048, 0x0049)
        };

    internal static bool Handles(ActionDefinition action) =>
        action.Kind is ActionKind.VisualEffect or ActionKind.PowerPlan;

    internal static bool IsSupportedEffect(string target) => Effects.ContainsKey(target);

    internal static ActionObservation Inspect(ActionDefinition action)
    {
        if (!OperatingSystem.IsWindows() || Environment.OSVersion.Version.Build < Math.Max(22000, action.MinimumBuild))
        {
            return new(action.Id, ActionState.NotApplicable, "Windows 11 is required for native settings.");
        }

        try
        {
            if (action.Kind == ActionKind.PowerPlan)
            {
                return InspectPowerPlan(action);
            }

            var enabled = ReadEffect(action.Target);
            return new(action.Id, enabled ? ActionState.Pending : ActionState.Configured,
                enabled
                    ? "Windows reports this animation as enabled."
                    : "Windows reports this individual animation setting as disabled. App-specific rendering may differ.");
        }
        catch (Exception error) when (error is Win32Exception or InvalidDataException)
        {
            return new(action.Id, ActionState.Failed, error.Message);
        }
    }

    internal static void Apply(ActionDefinition action)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows 11 is required.");
        }

        if (!WindowsAccess.IsAdministrator)
        {
            throw new InvalidOperationException("Administrator access is required to apply changes.");
        }

        var before = Inspect(action);
        if (before.State != ActionState.Pending)
        {
            throw new InvalidOperationException("Native setting is no longer eligible: " + before.Detail);
        }

        if (action.Kind == ActionKind.PowerPlan)
        {
            // Check the power source again immediately before switching plans.
            RequireExternalPower();
            var scheme = Guid.Parse(action.Target);
            ThrowIfError(PowerSetActiveScheme(IntPtr.Zero, ref scheme));
            return;
        }

        if (action.DesiredValue != 0 || !Effects.TryGetValue(action.Target, out var effect))
        {
            throw new InvalidDataException("Unsupported visual-effect operation.");
        }

        const uint updateProfileAndNotify = 0x01 | 0x02;
        if (action.Target == "window-animation")
        {
            var info = new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>(), MinAnimate = 0 };
            EnsureSuccess(SetAnimation(effect.Write, info.Size, ref info, updateProfileAndNotify));
        }
        else
        {
            EnsureSuccess(SetBoolean(effect.Write, 0, IntPtr.Zero, updateProfileAndNotify));
        }
    }

    private static bool ReadEffect(string target)
    {
        if (!Effects.TryGetValue(target, out var effect))
        {
            throw new InvalidDataException("Unsupported visual-effect target.");
        }

        if (target == "window-animation")
        {
            var info = new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>() };
            EnsureSuccess(GetAnimation(effect.Read, info.Size, ref info, 0));
            return info.MinAnimate != 0;
        }

        EnsureSuccess(GetBoolean(effect.Read, 0, out var enabled, 0));
        return enabled;
    }

    private static ActionObservation InspectPowerPlan(ActionDefinition action)
    {
        if (action.Target != HighPerformanceScheme)
        {
            throw new InvalidDataException("Unsupported power scheme.");
        }

        var desired = Guid.Parse(action.Target);
        var active = ReadActiveScheme();
        if (active == desired)
        {
            return new(action.Id, ActionState.Configured, "The High performance scheme is already active.");
        }

        if (!HasScheme(desired))
        {
            return new(action.Id, ActionState.NotApplicable,
                "The High performance scheme is not installed. No scheme will be created or unlocked.");
        }

        EnsureSuccess(GetSystemPowerStatus(out var status));
        if (status.AcLineStatus != 1 || status.SystemStatusFlag != 0)
        {
            return new(action.Id, ActionState.Blocked,
                "External power must be confirmed and Battery Saver must be off. Current plan: " + active);
        }

        return new(action.Id, ActionState.Pending,
            "External power detected. Current scheme: " + active + ". The installed High performance scheme can be selected.");
    }

    private static void RequireExternalPower()
    {
        EnsureSuccess(GetSystemPowerStatus(out var status));
        if (status.AcLineStatus != 1 || status.SystemStatusFlag != 0)
        {
            throw new InvalidOperationException("External power is unavailable or Battery Saver is active.");
        }
    }

    internal static PowerEntry ReadPowerState()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows is required to read power settings.");
        }

        EnsureSuccess(GetSystemPowerStatus(out var status));
        return new PowerEntry(
            ReadActiveScheme().ToString(),
            status.AcLineStatus == 255 ? null : status.AcLineStatus == 1,
            status.SystemStatusFlag > 1 ? null : status.SystemStatusFlag == 1);
    }

    private static Guid ReadActiveScheme()
    {
        ThrowIfError(PowerGetActiveScheme(IntPtr.Zero, out var pointer));
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidDataException("Windows returned no active power scheme.");
        }

        try
        {
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    private static bool HasScheme(Guid desired)
    {
        const uint accessScheme = 16;
        const uint noMoreItems = 259;
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        try
        {
            for (uint index = 0; ; index++)
            {
                var size = (uint)Marshal.SizeOf<Guid>();
                var result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, accessScheme, index, buffer, ref size);
                if (result == noMoreItems)
                {
                    return false;
                }

                ThrowIfError(result);
                if (size != Marshal.SizeOf<Guid>())
                {
                    throw new InvalidDataException("Unexpected power-scheme identifier size.");
                }

                if (Marshal.PtrToStructure<Guid>(buffer) == desired)
                {
                    return true;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void EnsureSuccess(bool success)
    {
        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void ThrowIfError(uint error)
    {
        if (error != 0)
        {
            throw new Win32Exception((int)error);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public uint Size;
        public int MinAnimate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetBoolean(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetBoolean(uint action, uint parameter, IntPtr value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetAnimation(uint action, uint parameter, ref AnimationInfo value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetAnimation(uint action, uint parameter, ref AnimationInfo value, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint access, uint index, IntPtr buffer, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
