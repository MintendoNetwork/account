using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text;
using BCrypt.Net;
using Npgsql;

public class PeopleController : nnasService
{
    // Alowed set of countries for account creation (this is refrenced from Pretendo's list of countries)
    private static readonly HashSet<string> AllowedAccountCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "JP", "AI", "AG", "AR", "AW", "BS", "BB", "BZ", "BO", "BR", "VG", "CA", "KY", "CL", "CO", "CR",
        "DM", "DO", "EC", "SV", "GF", "GD", "GP", "GT", "GY", "HT", "HN", "JM", "MQ", "MX", "MS", "AN",
        "NI", "PA", "PY", "PE", "KN", "LC", "VC", "SR", "TT", "TC", "US", "UY", "VI", "VE", "AL", "AU",
        "AT", "BE", "BA", "BW", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IS",
        "IE", "IT", "LV", "LS", "LI", "LT", "LU", "MK", "MT", "ME", "MZ", "NA", "NL", "NZ", "NO", "PL",
        "PT", "RO", "RU", "RS", "SK", "SI", "ZA", "ES", "SZ", "SE", "CH", "TR", "GB", "ZM", "ZW", "AZ",
        "MR", "ML", "NE", "TD", "SD", "ER", "DJ", "SO", "AD", "GI", "GG", "IM", "JE", "MC", "TW", "KR",
        "HK", "MO", "ID", "SG", "TH", "PH", "MY", "CN", "AE", "EG", "OM", "QA", "KW", "SA", "SY", "BH",
        "JO", "SM", "VA", "BM", "IN", "NG", "AO", "GH"
    };

    // Comments are from Pretendo
    private static readonly HashSet<string> AllowedAccountLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ja", "en", "fr", "de", "it", "es",
        // Likely simplified Chinese, but not seen in practice
        // Likely Korean, but not seen in practice
        "nl", "pt", "ru"
        // Likely traditional Chinese, but not seen in practice
    };
    private static long RandomUInt32()
    => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));


 /*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/people/USERNAME
    Description: Checks if a user exists already.
*/
    [HttpGet("people/{username}")]
    public async Task GetUser(string username)
    {

        string query = "SELECT EXISTS(SELECT 1 FROM mnids WHERE username = @Username)";

        try
        {

            using (NpgsqlConnection connection = new NpgsqlConnection(Settings.ConnectionString))
            {
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                
                    command.Parameters.AddWithValue("@Username", username);
                    await connection.OpenAsync();

                    object result = await command.ExecuteScalarAsync();
                    bool userExists = result is bool exists && exists;

                    if (userExists)
                    {
                        await Utils.CreateNNASError(HttpContext, 400, "0100", "Account ID already exists");
                        return;
                    }
                    else
                    {
                        HttpContext.Response.ContentType = "text/xml";
                        return;
                    }
                }
            }
        }
        catch (NpgsqlException pgEx)
        {
            System.Diagnostics.Debug.WriteLine($"pgex DB Error Details: {pgEx.ToString()}");
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
            throw;
        }
        catch (Exception ex)
        {
               System.Diagnostics.Debug.WriteLine($"DB Error Details: {ex.ToString()}");
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
            throw;
        }
    }

 /*
    [POST]
    Implementation of https://account.nintendo.net/v1/api/people
    Description: Registers a new account (MNID)
*/
    [HttpPost("people")]
    public async Task GetUser([FromBody] PersonDoc person)
    {
        string deviceIdStr = HttpContext.Request.Headers["X-Nintendo-Device-ID"].ToString();
        string serial = HttpContext.Request.Headers["X-Nintendo-Serial-Number"].ToString();
        string cert = HttpContext.Request.Headers["X-Nintendo-Device-Cert"].ToString();

        if (deviceIdStr == null || cert == null) 
        {
            await Utils.CreateNNASError(HttpContext, 401, "0113", "Unauthorized device", "device_id");
            return;
        }
        else if (serial == null) 
        {
            await Utils.CreateNNASError(HttpContext, 400, "0002", "serialNumber format is invalid");
            return;
        }
        else if (cert == null || person == null || string.IsNullOrEmpty(person?.UserId) || !AllowedAccountCountries.Contains(person.Country ?? "") || !AllowedAccountLanguages.Contains(person.Language ?? "") || string.IsNullOrEmpty(person.UserId) || string.IsNullOrEmpty(person.Password) || string.IsNullOrEmpty(person.BirthDate) || string.IsNullOrEmpty(person.Gender) || string.IsNullOrEmpty(person.Country) || string.IsNullOrEmpty(person.Language) || person.Region == 0 || string.IsNullOrEmpty(person.TzName) || person.Email == null || string.IsNullOrEmpty(person.Email.Address) || person.Mii == null || string.IsNullOrEmpty(person.Mii.Name))
        {
            await Utils.CreateNNASError(HttpContext, 400, "1600", "Unable to process request", "Bad Request");
            return;
        }
        else if (UsernameFilter.IsBlocked(person.UserId) || UsernameFilter.IsBlocked(person.Mii!.Name!))
        {
            await Utils.CreateNNASError(HttpContext, 400, "0101", "Account ID is not acceptable", "userId");
            return;
        }
    
        string username = person.UserId;

        string query = "SELECT EXISTS(SELECT 1 FROM mnids WHERE username = @Username)";

        string create = @"
            INSERT INTO mnids (
                pid, username, username_lower, password, birthdate, gender, country, language, region,
                timezone_name, timezone_offset,
                email_address, email_id, email_primary, email_parent, email_validated,
                email_code, email_token,
                mii_name, mii_primary, mii_data, mii_id, mii_hash, mii_image_id,
                flag_marketing, flag_off_device)
            VALUES (
                @Pid, @Username, @UsernameLower, @Password, @Birthdate, @Gender, @Country, @Language, @Region,
                @TimezoneName, @TimezoneOffset,
                @EmailAddress, @EmailId, @EmailPrimary, @EmailParent, @EmailValidated,
                @EmailCode, @EmailToken,
                @MiiName, @MiiPrimary, @MiiData, @MiiId, @MiiHash, @MiiImageId,
                @Marketing, @OffDevice)";

        string nexCreate = "INSERT INTO nex_accounts (pid, password) VALUES (@Pid, @Password)";

        string deviceCreate = @"
            INSERT INTO devices (model, device_id, serial, certificate_hash)
            VALUES (@model, @DeviceId, @Serial, @CertHash)
            ON CONFLICT (certificate_hash)
            DO UPDATE SET device_id = EXCLUDED.device_id, serial = EXCLUDED.serial
            RETURNING id;";

        try
        {

            using (NpgsqlConnection connection = new NpgsqlConnection(Settings.ConnectionString))
            {
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                
                    command.Parameters.AddWithValue("@Username", username);
                    await connection.OpenAsync();

                    object result = await command.ExecuteScalarAsync();
                    bool userExists = result is bool exists && exists;

                    if (userExists)
                    {
                        await Utils.CreateNNASError(HttpContext, 400, "0100", "Account ID already exists", "userId");
                        return;
                    }
                    else
                    {               
                    using (NpgsqlCommand cmd = new NpgsqlCommand(create, connection))
                    {
                        string nexPassword = Utils.GenerateNexPassword();
                        string lower = person.UserId.ToLowerInvariant();
                        string lower_pass = person.Password.ToLowerInvariant(); 
                        if (lower == lower_pass)
                        {
                            await Utils.CreateNNASError(HttpContext, 400, "1107", "Password cannot be the same as User ID", "password");
                            return;
                        }
                        // TODO: Maybe check if the pid exists beforehand to avoid collisions? The chances are very low but not zero. (1 in 1.8 billion chance of collision, so it's maybe not an issue :P )
                        int pid = new Random().Next(Utils.Min, Utils.Max);
                        string passwordHash = Utils.encryptNintendoHash(person.Password, pid);
                        string user_id = person.UserId;
                        long? offset = await Utils.GetTimezoneOffset(HttpContext, person.Country!, person.Language!, person.TzName!);
                        if (offset == null)
                        {
                            await Utils.CreateNNASError(HttpContext, 400, "1600", "Unable to process request", "tz_name");
                            return;
                        }

                        cmd.Parameters.AddWithValue("TimezoneOffset", offset.Value);
                        cmd.Parameters.AddWithValue("Pid", pid);
                        cmd.Parameters.AddWithValue("Username", user_id);
                        cmd.Parameters.AddWithValue("UsernameLower", user_id.ToLowerInvariant());
                        cmd.Parameters.AddWithValue("Password", passwordHash);
                        cmd.Parameters.AddWithValue("Birthdate", person.BirthDate!);
                        cmd.Parameters.AddWithValue("Gender", person.Gender!);
                        cmd.Parameters.AddWithValue("Country", person.Country!);
                        cmd.Parameters.AddWithValue("Language", person.Language!);
                        cmd.Parameters.AddWithValue("Region", person.Region);

                        cmd.Parameters.AddWithValue("TimezoneName", person.TzName);

                        cmd.Parameters.AddWithValue("EmailAddress", person.Email!.Address!.ToLowerInvariant());
                        cmd.Parameters.AddWithValue("EmailId", RandomUInt32());
                        cmd.Parameters.AddWithValue("EmailPrimary", person.Email.Primary == "Y");
                        cmd.Parameters.AddWithValue("EmailParent", person.Email.Parent == "Y");
                        cmd.Parameters.AddWithValue("EmailValidated", person.Email.Validated == "Y");
                        cmd.Parameters.AddWithValue("EmailCode", RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"));
                        cmd.Parameters.AddWithValue("EmailToken", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant());

                        cmd.Parameters.AddWithValue("MiiName", person.Mii!.Name!);
                        cmd.Parameters.AddWithValue("MiiPrimary", person.Mii.Primary == "Y");
                        cmd.Parameters.AddWithValue("MiiData", person.Mii.Data!);
                        cmd.Parameters.AddWithValue("MiiId", RandomUInt32());
                        cmd.Parameters.AddWithValue("MiiHash", Convert.ToHexString(RandomNumberGenerator.GetBytes(7)).ToLowerInvariant());
                        cmd.Parameters.AddWithValue("MiiImageId", RandomUInt32());

                        cmd.Parameters.AddWithValue("Marketing", person.MarketingFlag == "Y");
                        cmd.Parameters.AddWithValue("OffDevice", person.OffDeviceFlag == "Y");
                    
                        await cmd.ExecuteNonQueryAsync();
                        long deviceId = long.Parse(deviceIdStr);

                        // Hashes the certificate using SHA256 to store in the database
                        byte[] inputBytes = Encoding.UTF8.GetBytes(cert);
                        byte[] hashBytes = CryptographicOperations.HashData(HashAlgorithmName.SHA256, inputBytes);
                        string certHash = Convert.ToHexString(hashBytes);
                        int platform = int.Parse(HttpContext.Request.Headers["X-Nintendo-Platform-ID"]);
                        object? deviceRowIdObj;
                        await using (var insert = new NpgsqlCommand(deviceCreate, connection))
                        {
                            insert.Parameters.AddWithValue("DeviceId", deviceId);
                            insert.Parameters.AddWithValue("Serial", serial);
                            insert.Parameters.AddWithValue("CertHash", certHash);
                            insert.Parameters.AddWithValue("Model", platform switch { 0 => "ctr", 1 => "wup", _ => "unknown" });

                            deviceRowIdObj = await insert.ExecuteScalarAsync();
                        }

                        long deviceRowId = Convert.ToInt64(deviceRowIdObj);

                        string insertPidQuery = "INSERT INTO device_linked_pids (pid, device_id) VALUES (@Pid, @DeviceId)";
                        await using (var insert = new NpgsqlCommand(insertPidQuery, connection))
                        {
                            insert.Parameters.AddWithValue("Pid", pid);
                            insert.Parameters.AddWithValue("DeviceId", deviceRowId); // devices.id, not the ACTUAL device ID
                            await insert.ExecuteNonQueryAsync();
                        }


                        using (NpgsqlCommand nexCmd = new NpgsqlCommand(nexCreate, connection))
                        {
                            nexCmd.Parameters.AddWithValue("Pid", pid);
                            nexCmd.Parameters.AddWithValue("Password", nexPassword);
                            await nexCmd.ExecuteNonQueryAsync();
                        }

                        string PersonResponse =
                            "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n" +
                            "<person>\n" +
                            "    <pid>" + pid + "</pid>\n" +
                            "</person>";

                        await HttpContext.Response.WriteXmlAsync(PersonResponse, 201);
                        return;
                    }
                    }
                }
            }
        }
        catch (NpgsqlException pgEx)
        {
            System.Diagnostics.Debug.WriteLine($"pgex DB Error Details: {pgEx.ToString()}");
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
            throw;
        }
        catch (Exception ex)
        {
               System.Diagnostics.Debug.WriteLine($"DB Error Details: {ex.ToString()}");
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
            throw;
        }
    }

 /*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/people/@me/profile
    Description: Gets the user profile.
*/
    [HttpGet("people/@me/profile")]
    public async Task GetProfile()
    {

        // The PID of the logged-in user
        if (HttpContext.Items["pid"] is not int pid)
        {
            await Utils.CreateNNASError(HttpContext, 404, "0008", "Not Found");
            return;
        }

        string query = @"
            SELECT pid, username, birthdate, gender, country, language, region, timezone_name, timezone_offset,
                flag_active, flag_marketing, flag_off_device, created_at, updated_at,
                email_address, email_id, email_parent, email_primary, email_reachable,
                email_validated, email_validated_at,
                mii_name, mii_primary, mii_data, mii_id, mii_hash, mii_image_id
            FROM mnids WHERE pid = @Pid";

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(Settings.ConnectionString))
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Pid", pid);
                await connection.OpenAsync();

                using (NpgsqlDataReader rd = await command.ExecuteReaderAsync())
                {
                    if (!await rd.ReadAsync())
                    {
                        await Utils.CreateNNASError(HttpContext, 404, "0008", "Not Found");
                        return;
                    }

                    var row = new ProfileRow(
                        rd.GetInt32(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetString(5),
                        rd.GetInt32(6), rd.GetString(7), rd.GetInt32(8),
                        rd.GetBoolean(9), rd.GetBoolean(10), rd.GetBoolean(11), rd.GetDateTime(12), rd.GetDateTime(13),
                        rd.GetString(14), rd.GetInt64(15), rd.GetBoolean(16), rd.GetBoolean(17), rd.GetBoolean(18),
                        rd.GetBoolean(19), rd.IsDBNull(20) ? null : rd.GetDateTime(20),
                        rd.GetString(21), rd.GetBoolean(22), rd.GetString(23), rd.GetInt64(24), rd.GetString(25), rd.GetInt64(26));

                    string cdnBaseUrl = DotNetEnv.Env.GetString("CDN_BASE_URL", "https://example.com");
                    await HttpContext.Response.WriteXmlAsync(ProfileXml.Build(row, cdnBaseUrl));
                }
            }
        }
        catch (Exception ex)   // covers NpgsqlException too
        {
            System.Diagnostics.Debug.WriteLine($"DB Error Details: {ex}");
            await Utils.CreateNNASError(HttpContext, 500, "2001", "Internal server error");
        }
    }

}
