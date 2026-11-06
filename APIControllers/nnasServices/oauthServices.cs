using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text;
using BCrypt.Net;
using Npgsql;


public class OauthController : nnasService
{
    
    public static async Task CreateOauthResponse(HttpContext context, string accessToken, string refreshToken, int expiresIn = 3600)
    {
        string OauthResponse =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<OAuth20>\n" +
            "    <access_token>\n" +
            "        <token>" + accessToken + "</token>\n" +
            "        <refresh_token>" + refreshToken + "</refresh_token>\n" +
            "        <expires_in>" + expiresIn + "</expires_in>\n" +
            "    </access_token>\n" +
            "</OAuth20>";

        await context.Response.WriteXmlAsync(OauthResponse, 200);
    }
        string deviceCreate = @"
            INSERT INTO devices (model, device_id, serial, certificate_hash)
            VALUES (@Model, @DeviceId, @Serial, @CertHash)
            ON CONFLICT (certificate_hash)
            DO UPDATE SET device_id = EXCLUDED.device_id, serial = EXCLUDED.serial
            RETURNING id;";

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

/*
    [POST]
    Implementation of https://account.nintendo.net/v1/api/oauth20/access_token/generate
    Description: Gives a user an Oauth20 token, needed for most other stuff
*/

    [HttpPost("oauth20/access_token/generate")]
    public async Task HandleOauth(
        [FromForm] string? grant_type,
        [FromForm] string? user_id,
        [FromForm] string? password,
        [FromForm] string? refresh_token)
    {
        if (grant_type is not ("password" or "refresh_token"))
        {
            await Utils.CreateNNASError(HttpContext, 400, "0004", "Invalid Grant Type", "grant_type");
            return;
        }



        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        int pid;

        if (grant_type == "password")
        {
            if (string.IsNullOrWhiteSpace(user_id) || string.IsNullOrWhiteSpace(password))
            {
                await Utils.CreateNNASError(HttpContext, 400, "0106", "Invalid account ID or password");
                return;
            }

            string? storedHash = null;
            int foundPid = 0;

            await using (var cmd = new NpgsqlCommand(
                "SELECT pid, password FROM mnids WHERE username_lower = @name", connection))
            {
                cmd.Parameters.AddWithValue("name", user_id.ToLowerInvariant());
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    foundPid = reader.GetInt32(0);
                    storedHash = reader.GetString(1);
                }
            }

            if (storedHash is null || !BCrypt.Net.BCrypt.Verify(password, storedHash))
            {
                await Utils.CreateNNASError(HttpContext, 400, "0106", "Invalid account ID or password");
                return;
            }

            pid = foundPid;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(refresh_token))
            {
                await Utils.CreateNNASError(HttpContext, 400, "0106", "Invalid Refresh Token", "refresh_token");
                return;
            }
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM oauthtokens WHERE refresh_token = @refresh RETURNING pid", connection);
            cmd.Parameters.AddWithValue("refresh", Sha256Hex(refresh_token));

            if (await cmd.ExecuteScalarAsync() is not int found)
            {
                await Utils.CreateNNASError(HttpContext, 400, "0106", "Invalid Refresh Token", "refresh_token");
                return;
            }

            pid = found;
        }

        string accessToken     = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        string newRefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO oauthtokens (token, refresh_token, pid) VALUES (@token, @refresh, @pid)", connection))
        {
            insert.Parameters.AddWithValue("token", Sha256Hex(accessToken));
            insert.Parameters.AddWithValue("refresh", Sha256Hex(newRefreshToken));
            insert.Parameters.AddWithValue("pid", pid);
            await insert.ExecuteNonQueryAsync();
        }
        string device_id = HttpContext.Request.Headers["X-Nintendo-Device-ID"].ToString();
        string serial = HttpContext.Request.Headers["X-Nintendo-Serial-Number"].ToString();
        string cert = HttpContext.Request.Headers["X-Nintendo-Device-Cert"].ToString();
        int platform = int.Parse(HttpContext.Request.Headers["X-Nintendo-Platform-ID"]);

        long deviceId = long.Parse(device_id);

        // Hashes the certificate using SHA256 to store in the database
        byte[] inputBytes = Encoding.UTF8.GetBytes(cert);
        byte[] hashBytes = CryptographicOperations.HashData(HashAlgorithmName.SHA256, inputBytes);
        string certHash = Convert.ToHexString(hashBytes);

         // Check if the device already exists
        object? deviceRowIdObj;
        await using (var select = new NpgsqlCommand(
        "SELECT id FROM devices WHERE certificate_hash = @CertHash", connection))
        {
            select.Parameters.AddWithValue("CertHash", certHash);
            deviceRowIdObj = await select.ExecuteScalarAsync();
        }

        // Add if the device doesn't exist, or update it if it does
        if (deviceRowIdObj is null)
        {
            await using (var insert = new NpgsqlCommand(deviceCreate, connection))
            {
                insert.Parameters.AddWithValue("DeviceId", deviceId);
                insert.Parameters.AddWithValue("Serial", serial);
                insert.Parameters.AddWithValue("CertHash", certHash);
                insert.Parameters.AddWithValue("Model", platform switch { 0 => "ctr", 1 => "wup", _ => "unknown" });

                deviceRowIdObj = await insert.ExecuteScalarAsync();
            }
        }

        long deviceRowId = Convert.ToInt64(deviceRowIdObj);

        string insertPidQuery = "INSERT INTO device_linked_pids (pid, device_id) VALUES (@Pid, @DeviceId) ON CONFLICT DO NOTHING";
        await using (var insert = new NpgsqlCommand(insertPidQuery, connection))
        {
            insert.Parameters.AddWithValue("Pid", pid);
            insert.Parameters.AddWithValue("DeviceId", deviceRowId); // devices.id, not the ACTUAL device ID
            await insert.ExecuteNonQueryAsync();
        }
        await CreateOauthResponse(HttpContext, accessToken, newRefreshToken);
    }

}

