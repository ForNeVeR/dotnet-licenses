// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using TruePath;

namespace ReuseSpec;

public record ReuseFileEntry(
    AbsolutePath Path,
    ImmutableArray<string> LicenseIdentifiers,
    ImmutableArray<string> CopyrightStatements)
{
    /// <summary>Reads the REUSE information exclusively from the provided file.</summary>
    /// <remarks>Note it doesn't look into DEP5 or <c>.license</c> file.</remarks>
    internal static async Task<ReuseFileEntry?> ReadFromFile(AbsolutePath file)
    {
        if (!File.Exists(file.Value))
            return null;

        using var stream = File.OpenText(file.Value);

        // TODO[#46]: Support snippets as well
        // TODO[#46]: Support SPDX contributor info

        var text = await stream.ReadToEndAsync();
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var filteredLines = FilterIgnoredBlocks(lines);

        var (licenseIdentifiers, copyrightStatements) = CollectStatements(filteredLines);
        if (licenseIdentifiers.Count == 0 && copyrightStatements.Count == 0)
            return null;

        return new ReuseFileEntry(
            file,
            [..licenseIdentifiers],
            [..copyrightStatements]);
    }

    private static IEnumerable<string> FilterIgnoredBlocks(IEnumerable<string> input)
    {
        var ignoring = false;
        foreach (var line in input)
        {
            if (line.Contains("REUSE-IgnoreStart"))
            {
                ignoring = true;
                continue;
            }

            if (line.Contains("REUSE-IgnoreEnd"))
            {
                ignoring = false;
                continue;
            }

            if (!ignoring)
            {
                yield return line;
            }
        }
    }

    private static readonly Regex[] CopyrightPatterns = [
        new(@"SPDX-(?:File|Snippet)CopyrightText:\s*(.*)"),
        new(@"Copyright\s?(?:\([Cc]\))\s+(.*)"),
        new(@"©\s+(.*)")
    ];

    private static (List<string> Licenses, List<string> Copyrights) CollectStatements(IEnumerable<string> lines)
    {
        // TODO[#46]: Support inverted comment markers, see https://github.com/fsfe/reuse-tool/issues/343
        var licenses = new List<string>();
        var copyrights = new List<string>();
        foreach (var line in lines)
        {
            if (line.Contains("SPDX-License-Identifier:"))
            {
                licenses.Add(line.Split("SPDX-License-Identifier:", 2)[1].Trim());
                continue;
            }

            foreach (var pattern in CopyrightPatterns)
            {
                var match = pattern.Match(line);
                if (!match.Success) continue;

                copyrights.Add(match.Groups[1].Value);
            }
        }

        return (licenses, copyrights);
    }

    public static ReuseCombinedEntry CombineEntries(AbsolutePath baseDirectory, IEnumerable<ReuseFileEntry> entries)
    {
        var licenses = new List<string>();
        var copyrights = new List<string>();
        var licenseHash = new HashSet<string>();
        var copyrightHash = new HashSet<string>();
        foreach (var entry in entries.OrderBy(x => ((LocalPath)x.Path).RelativeTo(baseDirectory).Value))
        {
            licenses.AddRange(entry.LicenseIdentifiers.Where(license => licenseHash.Add(license)));
            copyrights.AddRange(entry.CopyrightStatements.Where(statement => copyrightHash.Add(statement)));
        }

        return new ReuseCombinedEntry([..licenses], [..copyrights]);
    }
}

public record ReuseCombinedEntry(
    ImmutableArray<string> LicenseIdentifiers,
    ImmutableArray<string> CopyrightStatements
);
