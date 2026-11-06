using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text;
using Npgsql;


public class ProviderController : nnasService
{
    public static async Task CreateServiceResponse(HttpContext context, string ServiceToken)
    {
        string ServiceResponse =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "    <service_token>\n" +
            "        <token>" + ServiceToken + "</token>\n" +
            "    </service_token>\n";

        await context.Response.WriteXmlAsync(ServiceResponse, 200);
    }

    public static async Task CreateNEXResponse(HttpContext context, string token, int port, string ip, string password, int pid)
    {
        string NEXResponse =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "    <nex_token>\n" +
            "        <host>" + ip + "</host>\n" +
            "        <nex_password>" + password + "</nex_password>\n" +
            "        <pid>" + pid + "</pid>\n" +
            "        <port>" + port + "</port>\n" +
            "        <token>" + token + "</token>\n" +
            "    </nex_token>\n";

        await context.Response.WriteXmlAsync(NEXResponse, 200);
    }

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

        private const string QueryServers = @"
            SELECT maintenance_mode, aes_key
            FROM servers WHERE client_id = @ClientId";

        private const string QueryServersNEX = @"
            SELECT maintenance_mode, aes_key, ip, port
            FROM servers WHERE game_id = @GameID";

/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/provider/service_token/@me
    Description: Gives the user a service token
*/
    [HttpGet("provider/service_token/@me")]
    public async Task HandleService([FromQuery] string? client_id)
    {

          // The PID of the logged-in user
        if (HttpContext.Items["pid"] is not int pid)
        {
            await Utils.CreateNNASError(HttpContext, 404, "0008", "Not Found");
            return;
        }

        string tid = HttpContext.Request.Headers["X-Nintendo-Title-ID"].ToString();
        if (client_id is null || tid is null)
        {
            await Utils.CreateNNASError(HttpContext, 400, "1022", "The requested client was not found.", "client_id");
            return;
        }

        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        await using var select = new NpgsqlCommand(QueryServers, connection);
        select.Parameters.AddWithValue("ClientId", client_id);

        await using var reader = await select.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            await Utils.CreateNNASError(HttpContext, 400, "1022", "The requested client was not found.", "client_id");
            return;
        }

        bool maintenance = reader.GetBoolean(reader.GetOrdinal("maintenance_mode"));
        if (maintenance)
        {
            await Utils.CreateNNASError(HttpContext, 503, "2002", "The requested game server is under maintenance");
            return;
        }

        // TODO: Add access level check
        var now = DateTimeOffset.UtcNow;
        byte[] token = Utils.CreateServiceToken(reader.GetString(reader.GetOrdinal("aes_key")), pid, tid, now, now.AddHours(1));
        string tokenhash = Convert.ToHexString(token).ToLowerInvariant();

        const string insertToken = @"
            INSERT INTO independenttokens (token, client_id, pid)
            VALUES (@Token, @ClientId, @Pid)";

        await using var cmd = new NpgsqlCommand(insertToken, connection);
        cmd.Parameters.AddWithValue("Token", tokenhash);
        cmd.Parameters.AddWithValue("ClientId", client_id);
        cmd.Parameters.AddWithValue("Pid", pid);

        await cmd.ExecuteNonQueryAsync();
        await CreateServiceResponse(HttpContext, Convert.ToBase64String(token));
    }

/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/provider/nex_token/@me
    Description: Gives the user nex server information with their NEX password
*/
    [HttpGet("provider/nex_token/@me")]
    public async Task HandleNEX([FromQuery] string? game_server_id)
    {
        // The PID of the logged-in user
        if (HttpContext.Items["pid"] is not int pid)
        {
            await Utils.CreateNNASError(HttpContext, 400, "0002", "Invalid access token", "access_token");
            return;
        }

        if (string.IsNullOrWhiteSpace(game_server_id))
        {
            await Utils.CreateNNASError(HttpContext, 400, "0118", "Unique ID and Game Server ID are not linked");
            return;
        }

        string tid = HttpContext.Request.Headers["X-Nintendo-Title-ID"].ToString().Trim().Replace("\0", "");
        if (string.IsNullOrEmpty(tid))
        {
            await Utils.CreateNNASError(HttpContext, 400, "1021", "The requested game server was not found");
            return;
        }



        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        string ip, aesKey;
        int port;

        await using (var select = new NpgsqlCommand(QueryServersNEX, connection))
        {
            select.Parameters.AddWithValue("GameID", game_server_id);
            await using var reader = await select.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                await Utils.CreateNNASError(HttpContext, 400, "1021", "The requested game server was not found");
                return;
            }

            ip     = reader.GetString(reader.GetOrdinal("ip"));
            port   = reader.GetInt32(reader.GetOrdinal("port"));
            aesKey = reader.GetString(reader.GetOrdinal("aes_key"));
        }


        string? nexPassword;
        await using (var selectUser = new NpgsqlCommand("SELECT password FROM nex_accounts WHERE pid = @Pid", connection))
        {
            selectUser.Parameters.AddWithValue("Pid", pid);
            nexPassword = await selectUser.ExecuteScalarAsync() as string;
        }

        if (nexPassword is null)
        {
            await Utils.CreateNNASError(HttpContext, 404, "0008", "Not Found");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        byte[] token = Utils.CreateServiceToken(aesKey, pid, tid, now, now.AddHours(1));
        string tokenHex = Convert.ToHexString(token).ToLowerInvariant();

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO nextokens (token, game_id, pid) VALUES (@Token, @GameID, @Pid)", connection))
        {
            insert.Parameters.AddWithValue("Token", tokenHex);
            insert.Parameters.AddWithValue("GameID", game_server_id);
            insert.Parameters.AddWithValue("Pid", pid);
            await insert.ExecuteNonQueryAsync();
        }

        await CreateNEXResponse(HttpContext, Convert.ToBase64String(token), port, ip, nexPassword, pid);
    }
}

