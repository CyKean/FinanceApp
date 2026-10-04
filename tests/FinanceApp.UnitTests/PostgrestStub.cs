namespace FinanceApp.UnitTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A stand-in for Supabase's PostgREST API, served over loopback HTTP so the real
/// <c>SupabaseClientProvider</c> and <c>SupabaseSyncService</c> run against it.
/// <para>
/// Rows are stored as the exact JSON the client posted, keyed by the id inside
/// that JSON. That is the behaviour that matters for this bug: an account is
/// identified by an id the client chose, so the stub can only hand a row back
/// under the id it arrived with. A client that mints a fresh id on the way in
/// makes a new row, exactly as the real database would.
/// </para>
/// </summary>
public sealed class PostgrestStub : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _tables =
        new(StringComparer.OrdinalIgnoreCase);

    public int Port { get; }

    public PostgrestStub()
    {
        for (var attempt = 0; ; attempt++)
        {
            Port = FreePort();
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            try
            {
                _listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < 30)
            {
                // Port already taken by something else; try another.
            }
        }

        _ = Task.Run(ServeAsync);
    }

    public string Url => $"http://127.0.0.1:{Port}";

    public int Count(string table) => _tables.TryGetValue(table, out var rows) ? rows.Count : 0;

    public IReadOnlyDictionary<string, string> Rows(string table) =>
        _tables.TryGetValue(table, out var rows)
            ? rows.ToDictionary(p => p.Key, p => p.Value)
            : new Dictionary<string, string>();

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                return;
            }

            try
            {
                await HandleAsync(context);
            }
            catch
            {
                // A stub that cannot answer must not take the test host down.
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var table = TableName(request.Url?.AbsolutePath);
        var method = request.HttpMethod;

        var body = "[]";
        context.Response.StatusCode = 200;

        if (method == "GET")
        {
            body = Select(table, request.Url?.Query);
        }
        else if (method == "POST")
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();
            var rows = _tables.GetOrAdd(table, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            foreach (var element in ParseElements(payload))
            {
                if (ReadString(element, "id") is { } id)
                    rows[id] = element.GetRawText();
            }

            // PostgREST answers an upsert with the affected rows.
            context.Response.StatusCode = 201;
            body = payload;
        }
        else if (method == "DELETE")
        {
            if (FilterValue(request.Url?.Query, "id") is { } id)
                _tables.GetValueOrDefault(table)?.TryRemove(id, out _);
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    /// <summary>
    /// Rows a select would return. Filters are applied, so a select scoped to one
    /// user cannot see another's rows - which is what row level security does.
    /// </summary>
    private string Select(string table, string? query)
    {
        if (!_tables.TryGetValue(table, out var rows))
            return "[]";

        var filters = ParseFilters(query);
        var matches = rows.Values
            .Where(row =>
            {
                using var document = JsonDocument.Parse(row);
                return filters.All(filter => ReadString(document.RootElement, filter.Key) == filter.Value);
            })
            .ToArray();

        return matches.Length == 0 ? "[]" : "[" + string.Join(",", matches) + "]";
    }

    private static string TableName(string? path) =>
        string.IsNullOrEmpty(path) ? "unknown" : path.Trim('/').Split('/')[^1];

    private static IEnumerable<JsonElement> ParseElements(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray()
            : new[] { document.RootElement.Clone() };
    }

    private static Dictionary<string, string> ParseFilters(string? query)
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
            return filters;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var value = Uri.UnescapeDataString(parts[1]);
            if (value.StartsWith("eq.", StringComparison.OrdinalIgnoreCase))
                filters[parts[0]] = value[3..];
        }

        return filters;
    }

    private static string? FilterValue(string? query, string column) =>
        ParseFilters(query).TryGetValue(column, out var value) ? value : null;

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* already stopped */ }
        _listener.Close();
        _cts.Dispose();
    }
}