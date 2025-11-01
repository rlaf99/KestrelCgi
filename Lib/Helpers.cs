using Microsoft.Extensions.Logging;

namespace KestrelCgi;

public class InvalidCgiOutputException : Exception
{
    public InvalidCgiOutputException(string? message)
        : base(message) { }
}

public class CgiHeaders
{
    const string ContentTypeLineStart = "Content-Type:";
    const string StatusLineStart = "Status:";
    const string LocationLineStart = "Location:";

    public string? ContentType { get; private set; }
    public string? Location { get; private set; }
    public int? Status { get; private set; }

    public void TraceValues(ILogger? logger)
    {
        logger?.LogTrace("CGI Header: {} : {}", nameof(ContentType), ContentType);
        logger?.LogTrace("CGI Header: {} : {}", nameof(Location), Location);
        logger?.LogTrace("CGI Header: {} : {}", nameof(Status), Status);
    }

    public bool TakeLine(string line)
    {
        if (line.StartsWith(ContentTypeLineStart))
        {
            if (ContentType is not null)
            {
                throw new InvalidCgiOutputException($"{nameof(ContentType)} already present");
            }
            ContentType = line[ContentTypeLineStart.Length..].TrimStart();

            return true;
        }
        else if (line.StartsWith(LocationLineStart))
        {
            if (Location is not null)
            {
                throw new InvalidCgiOutputException($"{nameof(Location)} already present");
            }
            Location = line[LocationLineStart.Length..].TrimStart();

            return true;
        }
        else if (line.StartsWith(StatusLineStart))
        {
            if (Status is not null)
            {
                throw new InvalidCgiOutputException($"{nameof(Status)} already present");
            }
            var statusInfo = line[StatusLineStart.Length..].TrimStart();
            var statusCodeInfo = statusInfo.Split(' ')[0];
            if (int.TryParse(statusCodeInfo, out var statusCode) == false)
            {
                throw new InvalidCgiOutputException($"Invalid {line}");
            }
            Status = statusCode;

            return true;
        }
        else
        {
            return false;
        }
    }
}

static class CgiRequestConstants
{
    internal const string AUTH_TYPE = "AUTH_TYPE";
    internal const string CONTENT_TYPE = "CONTENT_TYPE";
    internal const string CONTENT_LENGTH = "CONTENT_LENGTH";
    internal const string GATEWAY_INTERFACE = "GATEWAY_INTERFACE";
    internal const string PATH_INFO = "PATH_INFO";
    internal const string PATH_TRANSLATED = "PATH_TRANSLATED";
    internal const string QUERY_STRING = "QUERY_STRING";
    internal const string REMOTE_ADDR = "REMOTE_ADDR";
    internal const string REMOTE_HOST = "REMOTE_HOST";
    internal const string REMOTE_IDENT = "REMOTE_IDENT";
    internal const string REMOTE_USER = "REMOTE_USER";
    internal const string REQUEST_METHOD = "REQUEST_METHOD";
    internal const string SCRIPT_NAME = "SCRIPT_NAME";
    internal const string SERVER_NAME = "SERVER_NAME";
    internal const string SERVER_PORT = "SERVER_PORT";
    internal const string SERVER_PROTOCOL = "SERVER_PROTOCOL";
    internal const string SERVER_SOFTWARE = "SERVER_SOFTWARE";
}
