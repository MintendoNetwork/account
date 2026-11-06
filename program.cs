using Microsoft.AspNetCore.HttpLogging;

DotNetEnv.Env.Load(); 
var builder = WebApplication.CreateBuilder(args);
IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

Settings.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddControllers().AddXmlSerializerFormatters(); 

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod | 
                            HttpLoggingFields.RequestPath | 
                            HttpLoggingFields.ResponseStatusCode | 
                            HttpLoggingFields.Duration;
});
var app = builder.Build();

// initialize database
await DatabaseInitializer.InitializeAsync(Settings.ConnectionString);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/xml;charset=UTF-8";

        context.Response.Headers.Append("Server", "Nintendo 3DS (http)");
        context.Response.Headers.Append("X-Nintendo-Date", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

        string xmlResponse = 
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<errors>\n" +
            "    <error>\n" +
            "        <code>2001</code>\n" +
            "        <message>Internal Server Error</message>\n" +
            "    </error>\n" +
            "</errors>";

        byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(xmlResponse);
        context.Response.ContentLength = responseBytes.Length;
        await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
    });
});

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Server", "Nintendo 3DS (http)");
    context.Response.Headers.Append("X-Nintendo-Date", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    var start = DateTime.UtcNow;
    await next();
    var ms = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"{context.Request.Method} {context.Request.Path} {context.Response.StatusCode} {ms:0}ms");
});

app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == 404)
    {
    
        context.HttpContext.Response.ContentType = "application/xml;charset=UTF-8";

        string xmlResponse = 
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<errors>\n" +
            "    <error>\n" +
            "        <code>0008</code>\n" +
            "        <message>Not Found</message>\n" +
            "    </error>\n" +
            "</errors>";
        byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(xmlResponse);
        context.HttpContext.Response.ContentLength = responseBytes.Length;
        await context.HttpContext.Response.WriteAsync(xmlResponse);
    }

});
UsernameFilter.Load(Path.Combine(app.Environment.WebRootPath, "data", "blocklist.txt"));
app.UseStaticFiles();
app.MapControllers();
app.Run();