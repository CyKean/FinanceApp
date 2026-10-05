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
using System.Text.Json.Nodes;
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

    /// <summary>The anon key handed to the Supabase client, which is also what the
    /// SDK presents as <c>Authorization: Bearer</c> when nobody is signed in.</summary>
    public string AnonKey { get; } = "stub-anon-key";

    /// <summary>
    /// Applies the schema's row level security: the policies are
    /// <c>to authenticated</c> and keyed on <c>auth.uid() = user_id</c>, so a
    /// request that is not a signed-in user is refused writes and answered zero
    /// rows, exactly as Postgres would. Off by default - most tests are about what
    /// the client does with the rows, not about who is allowed to send them.
    /// </summary>
    public bool EnforceRowLevelSecurity { get; set; }

    /// <summary>Which user a request is authenticated as, read from the bearer
    /// token. Null when the bearer is the anon key, i.e. nobody is signed in.</summary>
    private string? SignedInUserId
    {
        get
        {
            var bearer = Headers.TryGetValue("Authorization", out var value) &&
                value.StartsWith("Bearer ", StringComparison.Ordinal)
                    ? value["Bearer ".Length..]
                    : null;

            return bearer is null || bearer == AnonKey ? null : SubjectOf(bearer);
        }
    }

    /// <summary>
    /// The <c>sub</c> claim of an unsigned JWT. The SDK checks that a session's
    /// token is well formed but does not verify its signature, so a token built
    /// this way is as good as a real one for testing who a request is sent as.
    /// </summary>
    public static string SessionTokenFor(string userId)
    {
        static string Base64Url(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var claims = $$"""{"sub":"{{userId}}","role":"authenticated","exp":4102444800}""";
        return $"{Base64Url("""{"alg":"none","typ":"JWT"}""")}.{Base64Url(claims)}.signature";
    }

    private static string? SubjectOf(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

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

    /// <summary>Request lines the client sent, in order - for asserting what the
    /// transport actually asks the server for.</summary>
    public IReadOnlyList<string> Requests => _requests.ToList();

    private readonly ConcurrentQueue<string> _requests = new();

    /// <summary>The last <c>Authorization</c> and <c>apikey</c> headers received,
    /// so a test can assert who the client actually presented.</summary>
    public ConcurrentDictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Count(string table) => _tables.TryGetValue(table, out var rows) ? rows.Count : 0;

    /// <summary>
    /// Puts a row on the server as it is, for fixtures that need to hand the
    /// client something no push of ours would produce - a row from before a
    /// migration, say, missing a column the current model expects.
    /// </summary>
    public void Put(string table, string id, string json) =>
        _tables.GetOrAdd(table, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase))[id] = json;

    /// <summary>
    /// Changes a row already on the server, in place. A null value takes the
    /// column out of the row altogether, which is how a table that has not run a
    /// migration yet answers: the column is not there, rather than there and null.
    /// </summary>
    public void Edit(string table, string id, params (string Field, object? Value)[] fields)
    {
        if (!_tables.TryGetValue(table, out var rows) || !rows.TryGetValue(id, out var source))
            throw new InvalidOperationException($"No {table} row {id} to edit.");

        var row = JsonNode.Parse(source)!.AsObject();
        foreach (var (field, value) in fields)
        {
            if (value is null)
            {
                row.Remove(field);
            }
            else if (value is JsonNode jsonNode)
            {
                row[field] = jsonNode;
            }
            else
            {
                row[field] = JsonSerializer.SerializeToNode(value);
            }
        }

        rows[id] = row.ToJsonString();
    }

    public IReadOnlyDictionary<string, string> Rows(string table) =>
        _tables.TryGetValue(table, out var rows)
            ? rows.ToDictionary(p => p.Key, p => p.Value)
            : new Dictionary<string, string>();

    /// <summary>
    /// Copies a row the client already posted under a fresh id, applying the
    /// given fields first - the shape of a row the build this replaces left
    /// behind: the same account, category or expense again, sitting in Supabase
    /// beside the original under an id nobody chose to keep. The first field
    /// must therefore be <c>id</c>.
    /// </summary>
    public string Duplicate(string table, string fromId, params (string Field, object? Value)[] fields)
    {
        if (!_tables.TryGetValue(table, out var rows) || !rows.TryGetValue(fromId, out var source))
            throw new InvalidOperationException($"No {table} row {fromId} to duplicate.");

        var row = JsonNode.Parse(source)!.AsObject();
        foreach (var (field, value) in fields)
        {
            if (value is JsonNode jsonNode)
                row[field] = jsonNode;
            else if (value is null)
                row[field] = null;
            else
                row[field] = JsonSerializer.SerializeToNode(value);
        }

        var id = row["id"]!.GetValue<string>();
        rows[id] = row.ToJsonString();
        return id;
    }

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
        _requests.Enqueue($"{method} {request.Url?.PathAndQuery}");
        foreach (var key in new[] { "Authorization", "apikey" })
            if (request.Headers[key] is { } value)
                Headers[key] = value;

        var body = "[]";
        context.Response.StatusCode = 200;

        var path = request.Url?.AbsolutePath ?? "";

        if (path.TrimEnd('/').EndsWith("/auth/v1/user", StringComparison.OrdinalIgnoreCase))
        {
            // GoTrue answers this from the bearer token, so the signed-in user is
            // whoever the client presented.
            body = $$"""{"id":"{{SignedInUserId ?? string.Empty}}","email":"signed-in@example.com","aud":"authenticated","role":"authenticated"}""";
        }
        else if (EnforceRowLevelSecurity && !Permits(method, body: null))
        {
            Refuse(context, table);
            return;
        }
        else if (method == "GET")
        {
            body = Select(table, request.Url?.Query);
        }
        else if (method == "POST")
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            if (EnforceRowLevelSecurity && !Permits(method, payload))
            {
                Refuse(context, table);
                return;
            }

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
            if (EnforceRowLevelSecurity && !Permits(method, body: null))
            {
                Refuse(context, table);
                return;
            }

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
    /// Whether this request would pass the schema's policies: a signed-in user, and
    /// a row stamped with their id. An anonymous request is refused every table,
    /// and a row belonging to someone else is refused on its own.
    /// </summary>
    private bool Permits(string method, string? body)
    {
        if (SignedInUserId is not { } caller)
            return false;

        // A read is scoped by the user_id filter the client already sends.
        if (method == "GET" || body is null)
            return true;

        foreach (var element in ParseElements(body))
        {
            if (ReadString(element, "user_id") is { } owner && owner != caller)
                return false;
        }

        return true;
    }

    /// <summary>Postgres's answer for a row the policies do not admit, verbatim -
    /// 42501 with the message the sync engine has to cope with.</summary>
    private void Refuse(HttpListenerContext context, string table)
    {
        var error = Encoding.UTF8.GetBytes(
            $$"""{"code":"42501","details":null,"hint":null,"message":"new row violates row-level security policy for table \"{{table}}\""}""");
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = error.Length;
        context.Response.OutputStream.Write(error, 0, error.Length);
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