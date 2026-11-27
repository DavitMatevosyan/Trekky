using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Trekky.Api.Tests;

/// <summary>
/// The fake GitHub's file contents (TREK-75): commits on a branch, each a full snapshot of the repository's files, with
/// git's blob SHAs, folder trees and the per-path commit history the docs viewer reads. A repository without commits
/// keeps the name-only folders and files of <see cref="FakeRepository"/>.
/// </summary>
public sealed partial class FakeGitHub
{
    private readonly ConcurrentDictionary<string, List<FakeSnapshot>> _histories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FakeSnapshot> _snapshots = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (FakeSnapshot Snapshot, string Folder)> _trees = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _blobRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _treeRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _failingRepositories = new(StringComparer.OrdinalIgnoreCase);
    private long _commitCounter;

    /// <summary>
    /// Commits <paramref name="changes"/> on <paramref name="branch"/>: each path set to its text, or removed when
    /// <c>null</c>. The branch starts empty. Returns the commit SHA.
    /// </summary>
    public string CommitFiles(
        string fullName, string branch, string message, IReadOnlyDictionary<string, string?> changes, string author = "Ada Lovelace")
        => CommitBytes(
            fullName,
            branch,
            message,
            changes.ToDictionary(c => c.Key, c => c.Value is null ? null : Encoding.UTF8.GetBytes(c.Value)),
            author);

    public string CommitBytes(
        string fullName, string branch, string message, IReadOnlyDictionary<string, byte[]?> changes, string author = "Ada Lovelace")
    {
        var history = _histories.GetOrAdd($"{fullName}@{branch}", _ => []);
        lock (history)
        {
            var files = history.Count == 0 ? ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal) : history[^1].Files;
            foreach (var (path, bytes) in changes)
            {
                if (bytes is null)
                {
                    files = files.Remove(path);
                }
                else
                {
                    var sha = BlobSha(bytes);
                    _blobs[sha] = bytes;
                    files = files.SetItem(path, sha);
                }
            }

            var commitSha = Sha1Hex($"commit {Interlocked.Increment(ref _commitCounter)} {fullName} {branch}");
            var snapshot = new FakeSnapshot(
                fullName, commitSha, message, author, DateTimeOffset.UtcNow.AddMinutes(-history.Count), files, changes.Keys.ToImmutableHashSet());
            _snapshots[commitSha] = snapshot;
            history.Add(snapshot);
            return commitSha;
        }
    }

    /// <summary>How often the blob <paramref name="sha"/> was downloaded.</summary>
    public int BlobRequests(string sha) => _blobRequests.GetValueOrDefault(sha);

    /// <summary>How often a tree of <paramref name="fullName"/> was listed.</summary>
    public int TreeRequests(string fullName) => _treeRequests.GetValueOrDefault(fullName);

    /// <summary>Makes every content request for the repository fail with 500, as when GitHub is down.</summary>
    public void FailContents(string fullName, bool failing = true)
    {
        if (failing)
        {
            _failingRepositories[fullName] = true;
        }
        else
        {
            _failingRepositories.TryRemove(fullName, out _);
        }
    }

    /// <summary>git's SHA-1 of a blob: <c>sha1("blob &lt;size&gt;\0" + bytes)</c>.</summary>
    public static string BlobSha(byte[] bytes)
    {
        var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes]));
    }

    private void MapDocs(WebApplication app)
    {
        app.MapGet("/repos/{owner}/{name}/commits", (HttpRequest request, string owner, string name, string sha, string path)
            => AsContentReader(request, owner, name, () =>
            {
                var history = _histories.GetValueOrDefault($"{owner}/{name}@{sha}");
                if (history is null)
                {
                    return Results.Json(new { message = $"No commit found for SHA: {sha}" }, statusCode: 422);
                }

                FakeSnapshot? last;
                lock (history)
                {
                    last = history.LastOrDefault(s => s.Changed.Any(c => c == path || c.StartsWith(path + "/", StringComparison.Ordinal)));
                }

                return Results.Json(last is null ? Array.Empty<object>() : [CommitJson(last)]);
            }));

        app.MapGet("/repos/{owner}/{name}/git/trees/{sha}", (HttpRequest request, string owner, string name, string sha)
            => AsContentReader(request, owner, name, () =>
            {
                _treeRequests.AddOrUpdate($"{owner}/{name}", 1, (_, n) => n + 1);
                if (!_trees.TryGetValue(sha, out var tree) || !string.Equals(tree.Snapshot.Repository, $"{owner}/{name}", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                var prefix = tree.Folder.Length == 0 ? string.Empty : tree.Folder + "/";
                var files = tree.Snapshot.Files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                var folders = files
                    .SelectMany(f => Ancestors(f.Key[prefix.Length..]))
                    .Distinct(StringComparer.Ordinal)
                    .Select(folder => (object)new { path = folder, type = "tree", sha = TreeSha(tree.Snapshot, prefix + folder) });
                var blobs = files.Select(f => (object)new { path = f.Key[prefix.Length..], type = "blob", sha = f.Value, size = _blobs[f.Value].Length });
                return Results.Json(new { sha, tree = folders.Concat(blobs).ToArray(), truncated = false });
            }));

        app.MapGet("/repos/{owner}/{name}/git/blobs/{sha}", (HttpRequest request, string owner, string name, string sha)
            => AsContentReader(request, owner, name, () =>
            {
                _blobRequests.AddOrUpdate(sha, 1, (_, n) => n + 1);
                return _blobs.TryGetValue(sha, out var bytes) && IsRaw(request)
                    ? Results.Bytes(bytes, "application/vnd.github.raw")
                    : Results.NotFound();
            }));
    }

    /// <summary>
    /// The contents API over a committed snapshot: a folder lists its entries, a file is its bytes when the raw media
    /// type is asked for. <c>null</c> when the repository has no commits on <paramref name="reference"/>.
    /// </summary>
    private IResult? SnapshotContents(HttpRequest request, string fullName, string path, string reference)
    {
        if (_failingRepositories.ContainsKey(fullName))
        {
            return Results.StatusCode(500);
        }

        if (Resolve(fullName, reference) is not { } snapshot)
        {
            return null;
        }

        path = path.Trim('/');
        if (snapshot.Files.TryGetValue(path, out var blob))
        {
            return IsRaw(request)
                ? Results.Bytes(_blobs[blob], "application/vnd.github.raw")
                : Results.Json(new { type = "file", name = path.Split('/')[^1], path, sha = blob });
        }

        var prefix = path.Length == 0 ? string.Empty : path + "/";
        var children = snapshot.Files.Keys
            .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Select(p => p[prefix.Length..].Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (children.Count == 0)
        {
            return Results.NotFound();
        }

        return Results.Json(children.Select(child =>
        {
            var childPath = prefix + child;
            return snapshot.Files.TryGetValue(childPath, out var sha)
                ? (object)new { type = "file", name = child, path = childPath, sha }
                : new { type = "dir", name = child, path = childPath, sha = TreeSha(snapshot, childPath) };
        }).ToArray());
    }

    private IResult AsContentReader(HttpRequest request, string owner, string name, Func<IResult> handle)
        => AsInstallation(request, id =>
            Reachable(id, owner, name) is null ? Results.NotFound()
            : _failingRepositories.ContainsKey($"{owner}/{name}") ? Results.StatusCode(500)
            : handle());

    private FakeSnapshot? Resolve(string fullName, string reference)
    {
        if (_histories.TryGetValue($"{fullName}@{reference}", out var history))
        {
            lock (history)
            {
                return history.LastOrDefault();
            }
        }

        return _snapshots.TryGetValue(reference, out var snapshot)
            && string.Equals(snapshot.Repository, fullName, StringComparison.OrdinalIgnoreCase) ? snapshot : null;
    }

    private object CommitJson(FakeSnapshot snapshot)
        => new
        {
            sha = snapshot.Sha,
            commit = new
            {
                message = snapshot.Message,
                author = new { name = snapshot.Author, date = snapshot.Date },
                tree = new { sha = TreeSha(snapshot, string.Empty) },
            },
        };

    /// <summary>A folder's tree SHA, from what it holds; remembered so the trees API can list it.</summary>
    private string TreeSha(FakeSnapshot snapshot, string folder)
    {
        var prefix = folder.Length == 0 ? string.Empty : folder + "/";
        var listing = string.Join('\n', snapshot.Files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(f => $"{f.Key[prefix.Length..]} {f.Value}"));
        var sha = Sha1Hex($"tree {snapshot.Repository}\n{listing}");
        _trees.TryAdd(sha, (snapshot, folder));
        return sha;
    }

    /// <summary>The folders above a path: <c>a/b/c.md</c> gives <c>a</c> and <c>a/b</c>.</summary>
    private static IEnumerable<string> Ancestors(string path)
    {
        var segments = path.Split('/');
        for (var i = 1; i < segments.Length; i++)
        {
            yield return string.Join('/', segments[..i]);
        }
    }

    private static bool IsRaw(HttpRequest request)
        => request.Headers.Accept.ToString().Contains("application/vnd.github.raw", StringComparison.Ordinal);

    private static string Sha1Hex(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record FakeSnapshot(
        string Repository,
        string Sha,
        string Message,
        string Author,
        DateTimeOffset Date,
        ImmutableSortedDictionary<string, string> Files,
        ImmutableHashSet<string> Changed);
}
