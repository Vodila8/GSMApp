using System.Diagnostics;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5127");
var app = builder.Build();

var bundledAdbPath = Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe");
var adbPath = Environment.GetEnvironmentVariable("ADB_PATH")
    ?? (File.Exists(bundledAdbPath) ? bundledAdbPath : "adb");
var allowedOrigin = Environment.GetEnvironmentVariable("AGENT_ALLOWED_ORIGIN") ?? "*";

app.Use(async (context, next) =>
{
    context.Response.Headers.AccessControlAllowOrigin = allowedOrigin;
    context.Response.Headers.AccessControlAllowMethods = "GET, OPTIONS";
    context.Response.Headers.AccessControlAllowHeaders = "Content-Type";
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new { service = "gsm-diagnostic-agent", status = "ready" }));
app.MapGet("/api/diagnostics", async () =>
{
    try
    {
        var result = await ScanAsync(adbPath);
        return Results.Ok(result);
    }
    catch (Exception exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

Console.WriteLine("GSM Diagnostic Agent is running on http://127.0.0.1:5127");
Console.WriteLine($"ADB path: {adbPath}");
Console.WriteLine("Keep this window open while the website uses device diagnostics.");
await app.RunAsync();

static async Task<DiagnosticResponse> ScanAsync(string adbPath)
{
    var devices = await RunAsync(adbPath, "devices");
    var deviceLine = devices.Split('\n')
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.EndsWith("\tdevice", StringComparison.Ordinal));
    if (deviceLine == null)
    {
        throw new InvalidOperationException("No authorized Android phone found. Connect the phone and allow USB debugging.");
    }

    var serial = deviceLine.Split('\t')[0];
    var manufacturer = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.manufacturer");
    var model = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.model");
    var productName = await RunOptionalAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.name");
    var androidVersion = await RunAsync(adbPath, "-s", serial, "shell", "getprop", "ro.build.version.release");
    var androidApiLevel = await RunOptionalAsync(adbPath, "-s", serial, "shell", "getprop", "ro.build.version.sdk");
    var cpuArchitecture = await RunOptionalAsync(adbPath, "-s", serial, "shell", "getprop", "ro.product.cpu.abi");
    var kernelVersion = await RunOptionalAsync(adbPath, "-s", serial, "shell", "uname", "-r");
    var selinuxStatus = await RunOptionalAsync(adbPath, "-s", serial, "shell", "getenforce");
    var usbConfiguration = await RunOptionalAsync(adbPath, "-s", serial, "shell", "getprop", "persist.sys.usb.config");
    var shellIdentity = await RunOptionalAsync(adbPath, "-s", serial, "shell", "id");
    var battery = await RunAsync(adbPath, "-s", serial, "shell", "dumpsys", "battery");
    var storage = await RunAsync(adbPath, "-s", serial, "shell", "df", "/data");
    var nfc = await RunOptionalAsync(adbPath, "-s", serial, "shell", "dumpsys", "nfc");
    var sensors = await RunOptionalAsync(adbPath, "-s", serial, "shell", "dumpsys", "sensorservice");
    var batteryLevel = ReadValue(battery, "level") ?? "Unknown";
    var health = HealthName(ReadValue(battery, "health") ?? "Unknown");
    var rawTemperature = ReadValue(battery, "temperature");
    var temperature = int.TryParse(rawTemperature, out var tenths)
        ? $"{tenths / 10.0:0.0} °C"
        : "Unknown";
    var storageText = FormatStorage(storage);
    var batteryVoltage = FormatMillivolts(ReadValue(battery, "voltage"));
    var chargingCurrent = FormatMicroamps(ReadValue(battery, "current now"));
    var usbPowered = ReadValue(battery, "USB powered")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    var usbStatus = usbPowered ? $"Connected · {DisplayUsbMode(usbConfiguration)}" : "Not powered over USB";
    var nfcStatus = nfc.Contains("mState=on", StringComparison.OrdinalIgnoreCase) ? "Available and enabled" : "Unavailable or disabled";
    var sensorStatus = FormatSensorStatus(sensors);
    var rootAccess = shellIdentity.StartsWith("uid=0", StringComparison.Ordinal) ? "Root shell available" : "Standard ADB shell (no root)";
    var details = $"{manufacturer} {model} — SN: {serial}";
    var report = $"Phone diagnostic — {DateTime.Now:yyyy-MM-dd HH:mm}\n" +
        $"Manufacturer: {manufacturer}\nModel: {model}\nAndroid version: {androidVersion}\nSerial number: {serial}\n" +
        $"Battery level: {batteryLevel}%\nBattery health: {health}\nBattery temperature: {temperature}\n" +
        $"Battery voltage: {batteryVoltage}\nCharging current: {chargingCurrent}\n" +
        $"Product code: {productName}\nAndroid API: {androidApiLevel}\nCPU architecture: {cpuArchitecture}\n" +
        $"Kernel: {kernelVersion}\nSELinux: {selinuxStatus}\nUSB: {usbStatus}\nNFC: {nfcStatus}\n" +
        $"Sensors: {sensorStatus}\nADB access: {rootAccess}\nInternal storage:\n{storageText}";

    return new DiagnosticResponse(true, manufacturer, model, productName, androidVersion, androidApiLevel, serial,
        cpuArchitecture, kernelVersion, selinuxStatus, usbStatus, batteryLevel, health, temperature, batteryVoltage,
        chargingCurrent, nfcStatus, sensorStatus, rootAccess, storageText, details, report);
}

static async Task<string> RunAsync(string fileName, params string[] arguments)
{
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };
    foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error.Trim());
    return output.Trim();
}

static async Task<string> RunOptionalAsync(string fileName, params string[] arguments)
{
    try { return await RunAsync(fileName, arguments); }
    catch { return "Unavailable"; }
}

static string? ReadValue(string output, string key) => Regex.Match(output, $@"^\s*{Regex.Escape(key)}:\s*(.+)$", RegexOptions.Multiline).Groups[1].Value.Trim() is { Length: > 0 } value ? value : null;
static string HealthName(string value) => value switch
{
    "2" => "Good",
    "3" => "Overheated",
    "4" => "Dead",
    "5" => "Over-voltage",
    "6" => "Unspecified failure",
    _ => value
};
static string FormatStorage(string output)
{
    var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
    if (line == null) return "Unavailable";
    var columns = Regex.Split(line, @"\s+");
    return columns.Length >= 5 ? $"Used: {columns[2]} KB\nAvailable: {columns[3]} KB\nUsage: {columns[4]}" : "Unavailable";
}

static string FormatMillivolts(string? value) => int.TryParse(value, out var millivolts)
    ? $"{millivolts / 1000.0:0.000} V" : "Unavailable";

static string FormatMicroamps(string? value) => long.TryParse(value, out var microamps)
    ? $"{microamps / 1000.0:0.0} mA" : "Unavailable";

static string DisplayUsbMode(string value) => string.IsNullOrWhiteSpace(value) || value == "Unavailable"
    ? "USB mode unavailable" : value.Replace(',', ' ');

static string FormatSensorStatus(string output)
{
    var match = Regex.Match(output, @"Total\s+(\d+)\s+h/w sensors,\s+(\d+)\s+running", RegexOptions.IgnoreCase);
    return match.Success ? $"{match.Groups[1].Value} hardware sensors · {match.Groups[2].Value} active" : "Unavailable";
}

public record DiagnosticResponse(
    bool Success, string Manufacturer, string Model, string ProductName, string AndroidVersion, string AndroidApiLevel,
    string SerialNumber, string CpuArchitecture, string KernelVersion, string SelinuxStatus, string UsbStatus,
    string BatteryLevel, string BatteryHealth, string BatteryTemperature, string BatteryVoltage, string ChargingCurrent,
    string NfcStatus, string SensorStatus, string RootAccess, string Storage, string DeviceDetails, string Report);
