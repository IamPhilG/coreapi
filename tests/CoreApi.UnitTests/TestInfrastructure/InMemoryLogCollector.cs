using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CoreApi.UnitTests.TestInfrastructure;

/// <summary>A single captured log entry, including its structured state and the scopes active when it was written.</summary>
public sealed record LogRecord(
    LogLevel Level,
    EventId EventId,
    string Category,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> Scopes);

/// <summary>
/// In-memory <see cref="ILoggerProvider"/> for asserting on what CoreApi logs (and, crucially,
/// what it does not). Captures message, level, structured state, exception, and active scopes so
/// tests can prove a correlation id is present, a secret/header is absent, and the right level was
/// used. State and scopes are materialized at capture time -- framework log states (e.g. the
/// request-starting log) read lazily from the HttpContext, which is disposed by the time a test
/// asserts.
/// </summary>
public sealed class InMemoryLogCollector : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogRecord> _records = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyList<LogRecord> Records => _records.ToArray();

    public ILogger CreateLogger(string categoryName) => new Collector(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose() => _records.Clear();

    /// <summary>Every human/structured string the collector saw -- messages, state values, and scope values.</summary>
    public IEnumerable<string> AllText()
    {
        foreach (LogRecord record in Records)
        {
            yield return record.Message;
            foreach (var pair in record.State)
                yield return $"{pair.Key}={pair.Value}";
            foreach (var scope in record.Scopes)
                foreach (var pair in scope)
                    yield return $"{pair.Key}={pair.Value}";
        }
    }

    public bool AnyTextContains(string value) =>
        AllText().Any(text => text.Contains(value, StringComparison.Ordinal));

    /// <summary>Distinct values seen for a scope key (e.g. "CorrelationId") across all captured entries.</summary>
    public IReadOnlyList<string> ScopeValues(string key)
    {
        var values = new List<string>();
        foreach (LogRecord record in Records)
            foreach (var scope in record.Scopes)
                foreach (var pair in scope)
                    if (pair.Key == key && pair.Value is { } v)
                        values.Add(v.ToString() ?? string.Empty);
        return values;
    }

    private sealed class Collector(string category, InMemoryLogCollector owner) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            owner._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Materialize now, while any HttpContext the state/scope reads from is still alive.
            IReadOnlyList<KeyValuePair<string, object?>> statePairs =
                state is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToList() : [];

            var scopes = new List<IReadOnlyList<KeyValuePair<string, object?>>>();
            owner._scopeProvider.ForEachScope(
                static (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopePairs)
                        list.Add(scopePairs.ToList());
                },
                scopes);

            owner._records.Enqueue(new LogRecord(
                logLevel, eventId, category, formatter(state, exception), exception, statePairs, scopes));
        }
    }
}
