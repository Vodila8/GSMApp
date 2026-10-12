using System.Diagnostics;
using System.Text.RegularExpressions;

namespace gsm.Services;

public class AdbDiagnosticService
{
    private readonly IConfiguration _configuration;
    private const string DefaultAdbPath = @"C:\Users\Andrey Pintyiskiy\Downloads\platform-tools\adb.exe";

    public AdbDiagnosticService(IConfiguration configuration) => _configuration = configuration;

    public async Task<AdbDiagnosticResult> ScanAsync()
    {
        var adbPath = _configuration["Adb:Path"] ?? DefaultAdbPath;
        if (!File.Exists(adbPath)) return AdbDiagnosticResult.Failure("ADB was not found. Check the configured ADB path.");

        var devices = await RunAsync(adbPath, "devices");
        var deviceLine = devices.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.EndsWith("\tdevice", StringComparison.Ordinal));
        if (deviceLine == null)
            return AdbDiagnosticResult.Failure("No authorized Android phone found. Connect a phone and allow USB debugging.");

        var serial = deviceLine.Split('\t')[0];
        var manufacturer = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.manufacturer");
        var model = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.model");
        var androidVersion = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.build.version.release");
        var battery = await RunAsync(adbPath, "-s", serial, "shell", "dumpsys", "battery");
        var storage = await RunAsync(adbPath, "-s", serial, "shell", "df", "/data");
        var batteryLevel = ReadValue(battery, "level") ?? "Unknown";
        var temperature = ReadValue(battery, "temperature");
        var health = ReadValue(battery, "health") ?? "Unknown";
        var healthText = health switch
        {
            "2" => "Good",
            "3" => "Overheated",
            "4" => "Dead",
            "5" => "Over-voltage",
            "6" => "Unspecified failure",
            _ => health
        };
        var temperatureText = int.TryParse(temperature, out var temperatureTenths) ? $"{temperatureTenths / 10.0:0.0} °C" : "Unknown";

        var storageText = FormatStorage(storage);
        var report = $"Phone diagnostic — {DateTime.Now:yyyy-MM-dd HH:mm}\n" +
            $"Manufacturer: {manufacturer}\nModel: {model}\nAndroid version: {androidVersion}\nSerial number: {serial}\n" +
            $"Battery level: {batteryLevel}%\nBattery health: {healthText}\nBattery temperature: {temperatureText}\n" +
            $"Internal storage:\n{storageText}";

        return new AdbDiagnosticResult(true, manufacturer, model, serial, androidVersion, batteryLevel, healthText, temperatureText, storageText, report, null);
    }

    private static async Task<string> RunAsync(string adbPath, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(adbPath) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }

    private static string? ReadValue(string output, string key) => Regex.Match(output, $@"^\s*{Regex.Escape(key)}:\s*(.+)$", RegexOptions.Multiline).Groups[1].Value.Trim() is { Length: > 0 } value ? value : null;

    private static string FormatStorage(string output)
    {
        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (line == null) return "Unavailable";

        var columns = Regex.Split(line, @"\s+");
        if (columns.Length < 5) return line;

        return $"Used: {columns[2]} KB\nAvailable: {columns[3]} KB\nUsage: {columns[4]}";
    }
}

public record AdbDiagnosticResult(
    bool Success,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string AndroidVersion,
    string BatteryLevel,
    string BatteryHealth,
    string BatteryTemperature,
    string Storage,
    string Report,
    string? Error)
{
    public static AdbDiagnosticResult Failure(string error) => new(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, error);
}
