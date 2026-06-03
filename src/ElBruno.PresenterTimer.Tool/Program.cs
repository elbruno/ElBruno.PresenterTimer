using System.Diagnostics;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ElBruno.PresenterTimer is only supported on Windows.");
    return 1;
}

var appExePath = Path.Combine(AppContext.BaseDirectory, "app", "ElBruno.PresenterTimer.exe");

if (!File.Exists(appExePath))
{
    Console.Error.WriteLine($"Could not find desktop app executable: {appExePath}");
    return 1;
}

var startInfo = new ProcessStartInfo(appExePath)
{
    UseShellExecute = true
};

foreach (var arg in args)
    startInfo.ArgumentList.Add(arg);

var process = Process.Start(startInfo);
if (process is null)
{
    Console.Error.WriteLine("Failed to launch ElBruno.PresenterTimer.");
    return 1;
}

return 0;
