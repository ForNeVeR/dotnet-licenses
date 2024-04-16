// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;
using DebianControlFileSpec;
using GitignoreParserNet;
using Microsoft.Extensions.FileSystemGlobbing;
using TruePath;

namespace ReuseSpec;

public static class ReuseDirectory
{
    public static async Task<List<ReuseFileEntry>> ReadEntries(AbsolutePath directory)
    {
        var allFiles = await EnumerateFiles(directory).ConfigureAwait(false);
        var dep5 = await ReadDep5File(directory).ConfigureAwait(false);
        var results = await Task.WhenAll(allFiles.Select(async file =>
        {
            var entry = await ReuseFileEntry.ReadFromFile(file).ConfigureAwait(false);
            if (entry != null)
                return entry;
            entry = await ReuseFileEntry.ReadFromFile(new AbsolutePath(file.Value + ".license")).ConfigureAwait(false);
            if (entry != null)
                return entry;

            return FindDep5Entry(dep5, directory, file);
        })).ConfigureAwait(false);
        return results.Where(x => x != null).ToList()!;
    }

    private static async Task<List<AbsolutePath>> EnumerateFiles(AbsolutePath directory)
    {
        await Task.Yield();

        var gitIgnorePath = directory / ".gitignore";
        if (!File.Exists(gitIgnorePath.Value))
        {
            return Directory.EnumerateFiles(directory.Value, "*", SearchOption.AllDirectories)
                .Select(x => new AbsolutePath(x))
                .ToList();
        }

        // TODO[#46]: Support nested .gitignore files on parent/child levels

        var (accepted, _) = GitignoreParser.Parse(gitIgnorePath.Value, Encoding.UTF8);
        return accepted
            .Where(x => !x.EndsWith("/"))
            .Select(x => directory / x)
            .Where(x => File.Exists(x.Value))
            .ToList();
    }

    private static async Task<List<DebianCopyrightFilesEntry>> ReadDep5File(AbsolutePath directory)
    {
        var dep5Path = directory / ".reuse/dep5";
        if (!File.Exists(dep5Path.Value))
            return [];

        using var stream = File.OpenText(dep5Path.Value);
        var file = await DebianControlFile.Read(stream).ConfigureAwait(false);

        var entries = file.Stanzas.Select(DebianCopyrightFilesEntry.Read).Where(x => x != null).ToList();

        // According to the spec: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
        // > Multiple Files stanzas are allowed. The last stanza that matches a particular file applies to it.
        // > More general stanzas should therefore be given first, followed by more specific overrides.
        // Thus, let's reverse the stanza order here, and choose the first corresponding stanza in the later code.
        entries.Reverse();
        return entries!;
    }

    private static ReuseFileEntry? FindDep5Entry(
        List<DebianCopyrightFilesEntry> dep5,
        AbsolutePath directory,
        AbsolutePath path)
    {
        // Mind the order from the comment in the ReadDep5File method.
        var entry = dep5.FirstOrDefault(x => x.Matcher.Match(directory.Value, path.Value).HasMatches);
        if (entry == null)
            return null;
        return new ReuseFileEntry(path, [entry.License], entry.Copyright.ToImmutableArray());
    }
}
