using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Account.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthenticateMnidAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        string? header = http.Request.Headers.Authorization.FirstOrDefault();

        if (header is null || !(header.StartsWith("Bearer") || header.StartsWith("Basic")))
        {
            await next();
            return;
        }

        string[] parts = header.Split(' ');
        string type = parts[0];
        string token = parts.Length > 1 ? parts[1] : "";

        int? pid = null;
        if (type == "Bearer")
            pid = await ValidateAccessTokenAsync(http, token);

        if (pid is null)
        {
            await Utils.CreateNNASError(http, 401, "0005", "Invalid access token", "access_token");
            context.Result = new EmptyResult();
            return;
        }
        http.Items["pid"] = pid.Value;
        await next();
    }

    private static async Task<int?> ValidateAccessTokenAsync(HttpContext http, string token)
    {


        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT pid FROM oauthtokens WHERE token = @token AND created_at >= NOW() - INTERVAL '1 hour'",
            connection);
        command.Parameters.AddWithValue("@token", tokenHash);

        object? result = await command.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }
}