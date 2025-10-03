using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AIM.WinUI.Services;

public sealed class IndexerService
{
    private readonly LogService _log;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<int>>> _content = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _nameIndex = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex TokenSplit = new("[^A-Za-z0-9]+", RegexOptions.Compiled);

    public IndexerService(LogService log) => _log = log;

    public async Task BuildAsync(string root, CancellationToken ct = default)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.csv", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            IndexFile(path);
        }
        foreach (var path in Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            IndexFile(path);
        }
    }

    public void IndexFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        // Content index for only csv/log; but we still name-index txt/csv/log
        bool contentOk = (ext == ".csv" || ext == ".log");
        try
        {
            // Clear old entries for this file
            Remove(path);

            if (contentOk)
            {
                var tokensByLine = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
                string? line; int lineNo = 0;
                while ((line = sr.ReadLine()) is not null)
                {
                    lineNo++;
                    foreach (var t in Tokenize(line))
                    {
                        if (!tokensByLine.TryGetValue(t, out var lst)) tokensByLine[t] = lst = new();
                        lst.Add(lineNo);
                    }
                }
                var dict = new ConcurrentDictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in tokensByLine) dict[kv.Key] = kv.Value;
                _content[path] = dict;
            }
            // Name index for csv/log/txt
            var nameTokens = Tokenize(Path.GetFileNameWithoutExtension(path));
            foreach (var t in nameTokens)
            {
                _nameIndex.AddOrUpdate(t, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path },
                    (_, set) => { set.Add(path); return set; });
            }
        }
        catch (Exception ex)
        {
            _log.Info("IndexError", new { path, ex.Message });
        }
    }

    public void Remove(string path)
    {
        _content.TryRemove(path, out _);
        foreach (var kv in _nameIndex) kv.Value.Remove(path);
    }

    public IEnumerable<string> Query(string query)
    {
        var terms = Tokenize(query).ToArray();
        if (terms.Length == 0) yield break;

        IEnumerable<string>? result = null;
        foreach (var t in terms)
        {
            var hits = _content.Where(kv => kv.Value.ContainsKey(t)).Select(kv => kv.Key);
            var nameHits = _nameIndex.TryGetValue(t, out var set) ? set : Enumerable.Empty<string>();
            var union = hits.Concat(nameHits).Distinct(StringComparer.OrdinalIgnoreCase);
            result = result is null ? union : result.Intersect(union, StringComparer.OrdinalIgnoreCase);
        }
        if (result is not null)
            foreach (var r in result) yield return r;
    }

    private static IEnumerable<string> Tokenize(string text) =>
        TokenSplit.Split(text).Where(s => s.Length > 0).Select(s => s.ToLowerInvariant());
}