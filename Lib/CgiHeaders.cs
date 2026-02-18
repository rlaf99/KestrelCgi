using System.Text;
using Microsoft.Extensions.Logging;

namespace KestrelCgi;

public class CgiHeaders(ILogger? logger = null)
{
    const string ContentTypeLineStart = "Content-Type:";
    const string StatusLineStart = "Status:";
    const string LocationLineStart = "Location:";

    public string? ContentType { get; private set; }
    public string? Location { get; private set; }
    public int? Status { get; private set; }

    public Dictionary<string, string> ResponseHeaders => _responseHeaders;

    Dictionary<string, string> _responseHeaders = [];

    StringBuilder _lineBuilder = new();

    public void Parse(Stream stream)
    {
        _lineBuilder.Clear();

        for (; ; )
        {
            var aByte = stream.ReadByte();
            if (aByte == -1)
            {
                throw new InvalidCgiOutputException("Header ends prematurely in CGI response");
            }

            if (aByte == '\n')
            {
                if (_lineBuilder.Length > 0 && _lineBuilder[_lineBuilder.Length - 1] == '\r')
                {
                    _lineBuilder.Remove(_lineBuilder.Length - 1, 1);
                }

                if (_lineBuilder.Length == 0)
                {
                    break;
                }

                var line = _lineBuilder.ToString();
                _lineBuilder.Clear();

                var valid = ParseCgiHeader(line) || ParseResponseHeader(line);
                if (valid == false)
                {
                    throw new InvalidCgiOutputException($"Invalid line in header: '{line}'");
                }
            }
            else
            {
                _lineBuilder.Append((char)aByte);
            }
        }
    }

    bool ParseResponseHeader(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1)
        {
            return false;
        }

        var name = line[..colonIndex];
        var value = line[(colonIndex + 1)..].TrimStart();

        _responseHeaders.Add(name, value);

        logger?.LogTrace("Response Header: {} : {}", name, value);

        return true;
    }

    public bool ParseCgiHeader(string line)
    {
        if (line.StartsWith(ContentTypeLineStart))
        {
            if (ContentType is not null)
            {
                throw new InvalidCgiOutputException($"{nameof(ContentType)} already present");
            }
            ContentType = line[ContentTypeLineStart.Length..].TrimStart();

            logger?.LogTrace("CGI Header: {} : {}", nameof(ContentType), ContentType);

            return true;
        }
        else if (line.StartsWith(LocationLineStart))
        {
            if (Location is not null)
            {
                throw new InvalidCgiOutputException($"{nameof(Location)} already present");
            }
            Location = line[LocationLineStart.Length..].TrimStart();

            logger?.LogTrace("CGI Header: {} : {}", nameof(Location), Location);

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

            logger?.LogTrace("CGI Header: {} : {}", nameof(Status), Status);

            return true;
        }
        else
        {
            return false;
        }
    }
}
