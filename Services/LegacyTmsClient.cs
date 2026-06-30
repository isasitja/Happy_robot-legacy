using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using TMS_API.Configuration;
using TMS_API.Models;

namespace TMS_API.Services
{
    /// <summary>
    /// Adapter over the HappyRobot Legacy TMS line-oriented TCP protocol (HR-LTMS-PR-001).
    /// Opens a fresh connection per request (connection reuse is not supported), builds the
    /// pipe-delimited request frame, reads the line-oriented response and translates ERR lines
    /// into <see cref="TmsProtocolException"/>.
    /// </summary>
    public class LegacyTmsClient : ILegacyTmsClient
    {
        // Maximum frame size including the \r\n terminator.
        private const int MaxFrameSize = 4096;
        private const string LineTerminator = "\r\n";

        private static readonly HashSet<string> KnownLoadFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "LOAD_ID", "ORIG_CITY", "ORIG_STATE", "ORIG_ZIP",
            "DEST_CITY", "DEST_STATE", "DEST_ZIP",
            "PICKUP_DT", "EQTYPE", "RATE", "MILES", "STATUS"
        };

        private static readonly string[] PickupDateFormats =
        {
            "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd"
        };

        private readonly LegacyTmsOptions _options;
        private readonly ILogger<LegacyTmsClient> _logger;

        public LegacyTmsClient(IOptions<LegacyTmsOptions> options, ILogger<LegacyTmsClient> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Load>> QueryLoadsAsync(LoadQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            var fields = new List<KeyValuePair<string, string>>();
            AddIfPresent(fields, "ORIG_CITY", parameters.OriginCity);
            AddIfPresent(fields, "ORIG_STATE", parameters.OriginState);
            AddIfPresent(fields, "ORIG_ZIP", parameters.OriginZip);
            AddIfPresent(fields, "DEST_CITY", parameters.DestinationCity);
            AddIfPresent(fields, "DEST_STATE", parameters.DestinationState);
            AddIfPresent(fields, "DEST_ZIP", parameters.DestinationZip);
            AddIfPresent(fields, "EQTYPE", parameters.Equipment);
            AddIfPresent(fields, "PICKUP_DT", parameters.PickupDate);
            if (parameters.MaxResults is int max)
            {
                AddIfPresent(fields, "MAX_RESULTS", max.ToString(CultureInfo.InvariantCulture));
            }

            if (fields.Count == 0)
            {
                // The server requires at least one filter; fail fast with the same wire code.
                throw new TmsProtocolException("MISSING_FIELD", "At least one filter is required for LOAD_QUERY.");
            }

            var frame = BuildFrame("LOAD_QUERY", fields);
            var records = await SendAsync(frame, allowRetry: true, cancellationToken);
            return records.Select(MapLoad).ToList();
        }

        public async Task<Load?> GetLoadAsync(string loadId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(loadId);

            var fields = new[] { new KeyValuePair<string, string>("LOAD_ID", loadId) };
            var frame = BuildFrame("LOAD_GET", fields);

            try
            {
                var records = await SendAsync(frame, allowRetry: true, cancellationToken);
                return records.Count == 0 ? null : MapLoad(records[0]);
            }
            catch (TmsProtocolException ex) when (ex.Code == "UNKNOWN_LOAD")
            {
                return null;
            }
        }

        public async Task<Load> BookLoadAsync(string loadId, LoadBookingRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(loadId);
            ArgumentNullException.ThrowIfNull(request);

            var fields = new List<KeyValuePair<string, string>>
            {
                new("LOAD_ID", loadId)
            };
            if (request.Rate is int rate)
            {
                // Wire format observed as zero-padded width 7 (e.g. RATE:0002150).
                AddIfPresent(fields, "RATE", rate.ToString("D7", CultureInfo.InvariantCulture));
            }
            if (request.AdditionalFields is not null)
            {
                foreach (var kv in request.AdditionalFields)
                {
                    AddIfPresent(fields, kv.Key.ToUpperInvariant(), kv.Value);
                }
            }

            var frame = BuildFrame("LOAD_BOOK", fields);

            // Booking is not idempotent (ALREADY_BOOKED), so never auto-retry.
            var records = await SendAsync(frame, allowRetry: false, cancellationToken);

            if (records.Count > 0)
            {
                return MapLoad(records[0]);
            }

            // Server acknowledged with just END; synthesize a minimal confirmation record.
            return new Load { LoadId = loadId, Status = "BOOKED" };
        }

        public async Task<string> EchoAsync(string message, CancellationToken cancellationToken = default)
        {
            var fields = new[] { new KeyValuePair<string, string>("MSG", message ?? string.Empty) };
            var frame = BuildFrame("DEBUG_ECHO", fields);

            // DEBUG_ECHO bypasses fault injection, so it is a safe connectivity probe.
            var records = await SendAsync(frame, allowRetry: true, cancellationToken);
            return string.Join(LineTerminator, records);
        }

        private string BuildFrame(string command, IEnumerable<KeyValuePair<string, string>> fields)
        {
            if (string.IsNullOrWhiteSpace(_options.Token))
            {
                throw new TmsProtocolException("AUTH_FAILED", "No LTMS auth token is configured.");
            }

            var sb = new StringBuilder();
            // CMD must appear first, AUTH is required on every request.
            sb.Append("CMD:").Append(command);
            ValidateValue("AUTH", _options.Token);
            sb.Append("|AUTH:").Append(_options.Token);

            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Value))
                {
                    continue;
                }

                ValidateValue(field.Key, field.Value);
                sb.Append('|').Append(field.Key).Append(':').Append(field.Value);
            }

            sb.Append(LineTerminator);
            var frame = sb.ToString();

            if (Encoding.ASCII.GetByteCount(frame) > MaxFrameSize)
            {
                throw new TmsProtocolException("MALFORMED", $"Request frame exceeds the {MaxFrameSize}-byte limit.");
            }

            return frame;
        }

        private static void ValidateValue(string key, string value)
        {
            if (value.IndexOf('|') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new TmsProtocolException("MALFORMED", $"Field '{key}' contains a reserved character ('|', CR or LF).");
            }
        }

        private static void AddIfPresent(ICollection<KeyValuePair<string, string>> fields, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        private async Task<IReadOnlyList<string>> SendAsync(string frame, bool allowRetry, CancellationToken cancellationToken)
        {
            var maxAttempts = allowRetry ? Math.Max(1, _options.MaxRetries + 1) : 1;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await ExecuteAsync(frame, cancellationToken);
                }
                catch (TmsProtocolException ex) when (allowRetry && attempt < maxAttempts && IsTransient(ex.Code))
                {
                    _logger.LogWarning("Transient LTMS fault {Code} on attempt {Attempt}/{Max}; retrying.", ex.Code, attempt, maxAttempts);
                }
                catch (Exception ex) when (allowRetry && attempt < maxAttempts && ex is IOException or SocketException)
                {
                    _logger.LogWarning(ex, "Transport error talking to LTMS on attempt {Attempt}/{Max}; retrying.", attempt, maxAttempts);
                }
            }
        }

        private static bool IsTransient(string code)
            => code is "SERVER_ERROR";

        private async Task<IReadOnlyList<string>> ExecuteAsync(string frame, CancellationToken cancellationToken)
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

            using var client = new TcpClient { NoDelay = true };

            try
            {
                await client.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TmsProtocolException("SERVER_ERROR", $"Timed out connecting to LTMS at {_options.Host}:{_options.Port}.");
            }
            catch (SocketException ex)
            {
                throw new TmsProtocolException("SERVER_ERROR", $"Unable to connect to LTMS at {_options.Host}:{_options.Port}: {ex.Message}");
            }

            using var ioCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ioCts.CancelAfter(TimeSpan.FromSeconds(_options.ReadTimeoutSeconds));
            var token = ioCts.Token;

            await using var stream = client.GetStream();

            var payload = Encoding.ASCII.GetBytes(frame);
            try
            {
                await stream.WriteAsync(payload, token);
                await stream.FlushAsync(token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TmsProtocolException("SERVER_ERROR", "Timed out sending request to LTMS.");
            }

            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var records = new List<string>();

            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TmsProtocolException("SERVER_ERROR", "Timed out reading the LTMS response.");
                }

                if (line is null)
                {
                    // Connection closed before a terminator: incomplete/implementation-defined response.
                    throw new TmsProtocolException("SERVER_ERROR", "Connection closed before the END terminator was received.");
                }

                if (line.Length == 0)
                {
                    continue;
                }

                if (line == "END")
                {
                    break;
                }

                if (line == "ERR" || line.StartsWith("ERR|", StringComparison.Ordinal))
                {
                    throw ParseError(line);
                }

                records.Add(line);
            }

            return records;
        }

        private static TmsProtocolException ParseError(string line)
        {
            var code = "SERVER_ERROR";
            var message = line;

            foreach (var token in line.Split('|'))
            {
                if (token.StartsWith("CODE:", StringComparison.Ordinal))
                {
                    code = token[5..].Trim();
                }
                else if (token.StartsWith("MSG:", StringComparison.Ordinal))
                {
                    message = token[4..].Trim();
                }
            }

            return new TmsProtocolException(code, message);
        }

        private static Load MapLoad(string line)
        {
            var fields = ParseFields(line);

            var load = new Load
            {
                LoadId = GetValue(fields, "LOAD_ID"),
                OriginCity = GetValue(fields, "ORIG_CITY"),
                OriginState = GetValue(fields, "ORIG_STATE"),
                OriginZip = GetValue(fields, "ORIG_ZIP"),
                DestinationCity = GetValue(fields, "DEST_CITY"),
                DestinationState = GetValue(fields, "DEST_STATE"),
                DestinationZip = GetValue(fields, "DEST_ZIP"),
                PickupDate = ParsePickupDate(GetValue(fields, "PICKUP_DT")),
                EquipmentType = GetValue(fields, "EQTYPE"),
                Rate = ParseInt(GetValue(fields, "RATE")),
                Miles = ParseInt(GetValue(fields, "MILES")),
                Status = GetValue(fields, "STATUS")
            };

            foreach (var kv in fields)
            {
                if (!KnownLoadFields.Contains(kv.Key))
                {
                    load.AdditionalFields[kv.Key] = kv.Value;
                }
            }

            return load;
        }

        private static IReadOnlyDictionary<string, string> ParseFields(string line)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in line.Split('|'))
            {
                if (token.Length == 0)
                {
                    continue;
                }

                var separator = token.IndexOf(':');
                if (separator < 0)
                {
                    continue;
                }

                var key = token[..separator];
                // Fixed-width fields are right-padded with spaces; trim the padding.
                var value = token[(separator + 1)..].TrimEnd();
                dict[key] = value;
            }

            return dict;
        }

        private static string GetValue(IReadOnlyDictionary<string, string> fields, string key)
            => fields.TryGetValue(key, out var value) ? value : string.Empty;

        private static int? ParseInt(string value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

        private static DateTime? ParsePickupDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParseExact(value, PickupDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : null;
        }
    }
}
