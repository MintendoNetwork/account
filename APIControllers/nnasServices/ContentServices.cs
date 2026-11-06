using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;

public class ContentController : nnasService
{
    public class TimeZoneItem
    {
        public string Area { get; set; } = "";
        public string Language { get; set; } = "";
        public string Name { get; set; } = "";
        public int UtcOffset { get; set; }
        public int Order { get; set; }
    }

/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/content/agreements/TYPE/REGION/VERSION
    Description: Returns the agreement for the given type, region, and version.
*/
    [HttpGet("content/agreements/{type}/{region}/{version}")]
    public async Task GetAgreements(string type, string region, string version)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        HttpContext.Response.StatusCode = 200;
        HttpContext.Response.ContentType = "application/xml;charset=UTF-8";
    
        // Maybe we don't need to check for the type, it would just be more authentic if we did.
        /* if (type != "Nintendo-Network-EULA")
        {
            await Utils.CreateNNASError(HttpContext, 400, "1101", $"No stored agreement found for this country: {region} type: {type} and version: {version}");
            return;
        } */

        // Gives newest version based on the highest number folder (wwroot/terms-policy/[NUMBER]/[REGION]-terms-privacy.xml)
        if (version == "@latest")
        {
            string baseTermsPath = Path.Combine(env.WebRootPath, "terms-policy");
            string agreementFilePath = Utils.GetAgreementFilePath(region, baseTermsPath);
            if (agreementFilePath == null || !System.IO.File.Exists(agreementFilePath))
            {
                await Utils.CreateNNASError(HttpContext, 400, "1101", $"No stored agreement found for this country: {region} type: {type} and version: {version}");
                return;
            }

            byte[] Agreement = await System.IO.File.ReadAllBytesAsync(agreementFilePath);
            HttpContext.Response.ContentLength = Agreement.Length;
            await HttpContext.Response.Body.WriteAsync(Agreement, 0, Agreement.Length);
            return;
        }
        else if (int.TryParse(version, out int versionNumber))
        {
            string baseTermsPath = Path.Combine(env.WebRootPath, "terms-policy");
            string agreementFilePath = Path.Combine(baseTermsPath, versionNumber.ToString("D4"), region + "-terms-privacy.xml");

            if (!System.IO.File.Exists(agreementFilePath))
            {
                await Utils.CreateNNASError(HttpContext, 400, "1101", $"No stored agreement found for this country: {region} type: {type} and version: {version}");
                return;
            }

            byte[] Agreement = await System.IO.File.ReadAllBytesAsync(agreementFilePath);

            HttpContext.Response.ContentLength = Agreement.Length;

            await HttpContext.Response.Body.WriteAsync(Agreement, 0, Agreement.Length);
            return;
        }
        else
        {
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
            return;
        }
    }

/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/content/time_zones/COUNTRY/LANGUAGE
*/
    [HttpGet("content/time_zones/{countryCode}/{language}")]
    public async Task GetTimeZones(string countryCode, string language)
    {

        HttpContext.Response.StatusCode = 200;
        HttpContext.Response.ContentType = "application/xml;charset=UTF-8";

        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        string jsonFilePath = Path.Combine(env.WebRootPath, "data", "timezones.json");

        if (!System.IO.File.Exists(jsonFilePath))
        {
            await Utils.CreateNNASError(HttpContext, 404, "0008", "Not found");
            return;
        }

        string jsonString = await System.IO.File.ReadAllTextAsync(jsonFilePath);
            
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true,NumberHandling = JsonNumberHandling.AllowReadingFromString };            
        var timezoneDatabase = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<TimeZoneItem>>>>(jsonString, jsonOptions);

        if (timezoneDatabase == null || !timezoneDatabase.TryGetValue(countryCode, out var languageDict) || languageDict.Count == 0)
        {
            await Utils.CreateNNASError(HttpContext, 404, "0008", "Not found");
            return;
        }

        if (!languageDict.TryGetValue(language, out var selectedTimezones))
        {
            selectedTimezones = languageDict.Values.First();
        }

        var xmlBuilder = new StringBuilder();
        xmlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlBuilder.AppendLine("<timezones>");

        foreach (var tz in selectedTimezones)
        {
            xmlBuilder.AppendLine("    <timezone>");
            xmlBuilder.AppendLine($"        <area>{tz.Area}</area>");
            xmlBuilder.AppendLine($"        <language>{tz.Language}</language>");
            xmlBuilder.AppendLine($"        <name>{tz.Name}</name>");
            xmlBuilder.AppendLine($"        <utc_offset>{tz.UtcOffset}</utc_offset>");
            xmlBuilder.AppendLine($"        <order>{tz.Order}</order>");
            xmlBuilder.AppendLine("    </timezone>");
        }

        xmlBuilder.AppendLine("</timezones>");
        byte[] Agreement = Encoding.UTF8.GetBytes(xmlBuilder.ToString());
        HttpContext.Response.ContentLength = Agreement.Length;

        await HttpContext.Response.Body.WriteAsync(Agreement, 0, Agreement.Length);
    }
}
