// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

using DebianControlFileSpec;
using Microsoft.Extensions.FileSystemGlobbing;

namespace ReuseSpec;

internal record DebianCopyrightFilesEntry(
    Matcher Matcher,
    string[] Copyright,
    string License)
{
    public static DebianCopyrightFilesEntry? Read(Stanza stanza)
    {
        var value = stanza.Fields.ToDictionary(tuple => tuple.Item1);
        if (!value.TryGetValue("Files", out var filesField))
        {
            return null;
        }

        var (_, files) = filesField;
        var matcher = new Matcher();
        matcher.AddIncludePatterns(files.Split("\n", StringSplitOptions.RemoveEmptyEntries));
        return new DebianCopyrightFilesEntry(matcher, value["Copyright"].Item2.Split("\n"), value["License"].Item2);
    }
}
