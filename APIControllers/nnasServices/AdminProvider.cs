using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using Npgsql;


public class AdminController : nnasService
{
    public static async Task CreateMappedResponse(HttpContext context, string? mappedids)
    {
        string MappedResponse =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "    <mapped_ids>\n" +
            mappedids +
            "    </mapped_ids>\n";

        await context.Response.WriteXmlAsync(MappedResponse, 200);
    }

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private const string querypid = @"
        SELECT pid, username
        FROM mnids WHERE pid = ANY(@Pids)";

    private const string queryname = @"
        SELECT pid, username
        FROM mnids WHERE username = ANY(@Usernames)";

/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/admin/mapped_ids
    Description: Returns pid(s) or username(s) for users to be identified
*/
    [HttpGet("admin/mapped_ids")]
    public async Task HandleMap([FromQuery] string? input_type, [FromQuery] string? output_type, [FromQuery] string? input)
    {
        if (input_type is null || output_type is null || input is null)
        {
            await Utils.CreateNNASError(HttpContext, 400, "1600", "Unable to process request", "Bad Request");
            return;
        }
        
        var xmlBuilder = new StringBuilder();
        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        if (input_type == "user_id") 
        {
            string[] userIds = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            await using var select = new NpgsqlCommand(queryname, connection);
            
            select.Parameters.AddWithValue("Usernames", userIds);
            
            await using var reader = await select.ExecuteReaderAsync();
            

            int foundCount = 0;

            var databaseMatches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                string dbUsername = reader.GetString(reader.GetOrdinal("username"));
                string dbPid = reader.GetInt32(reader.GetOrdinal("pid")).ToString();
                databaseMatches[dbUsername] = dbPid;
            }
            foreach (var input_for in userIds)
            {
                xmlBuilder.AppendLine("  <mapped_id>");
                xmlBuilder.AppendLine($"    <in_id>{System.Security.SecurityElement.Escape(input_for)}</in_id>");
                if (databaseMatches.TryGetValue(input_for, out string currentPid))
                {
                    xmlBuilder.AppendLine($"    <out_id>{System.Security.SecurityElement.Escape(currentPid)}</out_id>");
                }
                else
                {
                    xmlBuilder.AppendLine("    <out_id></out_id>");
                }

                xmlBuilder.AppendLine("  </mapped_id>");
            }


            await CreateMappedResponse(HttpContext, xmlBuilder.ToString());
            return;
        }
        
        else if (input_type == "pid") 
        {
            int[] userpids = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(str => int.TryParse(str, out int val) ? (int?)val : null)
                    .Where(val => val != null)
                    .Select(val => val.Value)
                    .ToArray();

            if (userpids.Length == 0)
            {
                await Utils.CreateNNASError(HttpContext, 400, "1022", "No valid user IDs provided.", "client_id");
                return;
            }
            
            await using var select = new NpgsqlCommand(querypid, connection);
            select.Parameters.AddWithValue("Pids", userpids);
            
            await using var reader = await select.ExecuteReaderAsync();
            
            var databaseMatches = new Dictionary<int, string>();
            while (await reader.ReadAsync())
            {
                int dbPid = reader.GetInt32(reader.GetOrdinal("pid"));
                string dbUsername = reader.GetString(reader.GetOrdinal("username"));
                
                databaseMatches[dbPid] = dbUsername;
            }

            string[] pids = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var input_for in pids)
            {
                xmlBuilder.AppendLine("  <mapped_id>");
                xmlBuilder.AppendLine($"    <in_id>{System.Security.SecurityElement.Escape(input_for)}</in_id>");

                if (int.TryParse(input_for, out int int_pid) && databaseMatches.TryGetValue(int_pid, out string currentUsername))
                {
                    // Found a matching username
                    xmlBuilder.AppendLine($"    <out_id>{System.Security.SecurityElement.Escape(currentUsername)}</out_id>");
                }
                else
                {
                    // pid wasn't found or wasn't a valid number
                    xmlBuilder.AppendLine("    <out_id></out_id>");
                }

                xmlBuilder.AppendLine("  </mapped_id>");
            }

            await CreateMappedResponse(HttpContext, xmlBuilder.ToString());
            return;
        }

        else 
        {
            await Utils.CreateNNASError(HttpContext, 400, "1600", "Unable to process request", "Bad Request");
            return;       
        }

 
    }
/*
    [GET]
    Implementation of https://account.nintendo.net/v1/api/admin/time
    Description: Returns nothing, just gives time from Date and X-Nintendo-Date
*/
   [HttpGet("admin/time")]
    public async Task HandleTime()
    {
        // We don't have to do anything here because everything is already in the headers by default :P
        return;
 
    }
}

