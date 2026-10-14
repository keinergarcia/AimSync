namespace AimSync.Test;

internal static class ViGEmUtil
{
    public static bool IsBusInstalled()
    {
        var sysFile = Path.Combine(Environment.SystemDirectory, "drivers", "ViGEmBus.sys");
        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Nefarius Software Solutions", "ViGEm Bus Driver");
        return File.Exists(sysFile) && Directory.Exists(installDir);
    }
}