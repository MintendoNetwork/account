using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;

public static class Utils
{   // Min and Max values for Nintendo PIDs
    public const int Min = 1_000_000_000;
    public const int Max = 1_799_999_999;
    public class TimeZoneItem
    {
        [JsonPropertyName("area")]       public string Area { get; set; } = "";
        [JsonPropertyName("utc_offset")] public long UtcOffset { get; set; }
    }

    // Error code maker for NNAS
    public static async Task CreateNNASError(HttpContext context, int statusCode, string errorCode, string message, string? cause = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/xml;charset=UTF-8";
        string causeXml = !string.IsNullOrEmpty(cause) ? $"    <cause>{cause}</cause>\n" : "";
        string xmlResponse = 
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<errors>\n" +
            "    <error>\n" +
            causeXml +
            "        <code>" + errorCode + "</code>\n" +
            "        <message>" + message + "</message>\n" +
            "    </error>\n" +
            "</errors>";

        byte[] responseBytes = Encoding.UTF8.GetBytes(xmlResponse);
        context.Response.ContentLength = responseBytes.Length;
        await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
    }

    public static string GetAgreementFilePath(string country,string baseTermsPath)
    {
    string highestVersionFolder = Directory.GetDirectories(baseTermsPath)
        .Select(Path.GetFileName)
        .Where(name => int.TryParse(name, out _)) // Filters out any non-numeric folders
        .OrderByDescending(name => int.Parse(name)) // Sorts numerically highest to lowest
        .FirstOrDefault(); // Grabs the top one (e.g., "0002")

    if (string.IsNullOrEmpty(highestVersionFolder))
    {
        return null; // No valid version folders found
    }
    string finalFilePath = Path.Combine(baseTermsPath, highestVersionFolder, country + "-terms-privacy.xml");
    return finalFilePath;
    }

    // Makes nintendo password hash to be salted for login verification
    public static string NintendoPasswordHash(string password, int pid)
    {
        byte[] pidBytes = BitConverter.GetBytes(pid);
        byte[] magic = { 0x02, 0x65, 0x43, 0x46 };
        byte[] pw = Encoding.UTF8.GetBytes(password);

        byte[] data = pidBytes.Concat(magic).Concat(pw).ToArray();
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }  

    // Things get messy here :P
    public static async Task<long?> GetTimezoneOffset(HttpContext context, string countryCode, string language, string timezone)
    {
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        string path = Path.Combine(env.WebRootPath, "data", "timezones.json");

        if (!File.Exists(path))
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        var db = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<TimeZoneItem>>>>(
            await File.ReadAllTextAsync(path), options);

        if (db == null || !db.TryGetValue(countryCode.ToUpperInvariant(), out var languages) || languages.Count == 0)
            return null;

        if (!languages.TryGetValue(language.ToLowerInvariant(), out var items))
            items = languages.Values.First();

        return items.FirstOrDefault(t => string.Equals(t.Area, timezone, StringComparison.OrdinalIgnoreCase))?.UtcOffset;
    }

    // encrypts hash for storage
    public static string encryptNintendoHash(string password, int pid)
        => BCrypt.Net.BCrypt.HashPassword(NintendoPasswordHash(password, pid), workFactor: 10);
    

    // Generates a random password for the user's NEX account
    public static string GenerateNexPassword()
    {
        const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        return RandomNumberGenerator.GetString(Chars, 16);
    }

    // Creates service tokens for /v1/api/provider/service_token/@me and nex_token aswell
    public static byte[] CreateServiceToken(string key, int pid, string titleId, DateTimeOffset issued, DateTimeOffset expires)
    {
        byte[] data = new byte[28];

        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0, 4), (uint)pid);
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(4, 8), Convert.ToUInt64(titleId, 16));
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(12, 8), (ulong)issued.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(20, 8), (ulong)expires.ToUnixTimeMilliseconds());

        byte[] hmac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), data);

        return [.. data, .. hmac];
    }
}