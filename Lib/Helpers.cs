namespace KestrelCgi;

public class InvalidCgiOutputException : Exception
{
    public InvalidCgiOutputException(string? message)
        : base(message) { }
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
