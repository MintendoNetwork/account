using System.Text;
// Writes kool xml responses without chunking!!
public static class ResponseExtensions
{
    public static async Task WriteXmlAsync(this HttpResponse response, string xml, int statusCode = 200)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        response.StatusCode = statusCode;
        response.ContentType = "application/xml;charset=UTF-8";
        response.ContentLength = bytes.Length;
        await response.Body.WriteAsync(bytes);
    }
}