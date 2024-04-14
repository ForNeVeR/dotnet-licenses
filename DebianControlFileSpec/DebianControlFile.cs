// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

﻿namespace DebianControlFileSpec;

public class DebianControlFile(List<Stanza> stanzas)
{
    private static readonly char[] Separator = [':'];
    public static async Task<DebianControlFile> Read(StreamReader stream)
    {
        var text = await stream.ReadToEndAsync().ConfigureAwait(false);
        var lines = text.ReplaceLineEndings("\n").Split("\n");

        var stanzas = new List<Stanza>();
        Stanza? currentStanza = null;
        void EndStanza()
        {
            if (currentStanza != null)
                stanzas.Add(currentStanza);
            currentStanza = null;
        }
        void AppendContinuationValue(string line)
        {
            line = line.Trim();
            if (currentStanza == null)
                throw new Exception($"No stanza to append value to: \"{line}\".");
            if (currentStanza.Fields.Count == 0)
                throw new Exception($"No stanza field to append value to: \"{line}\".");
            var (key, value) = currentStanza.Fields[^1];
            currentStanza.Fields[^1] = (key, value + "\n" + line);
        }

        void AppendNewValue(string key, string value)
        {
            currentStanza ??= new Stanza();
            currentStanza.Fields.Add((key, value));
        }

        foreach (var line in lines)
        {
            if (line.StartsWith('#')) continue;
            if (line.Trim().Length == 0)
                EndStanza();

            if (line.StartsWith(' '))
                AppendContinuationValue(line);

            var components = line.Split(Separator, 2);
            if (components.Length != 2)
                throw new Exception($"Format error: line doesn't have separator: \"{line}\".");
            var key = components[0];
            var value = components[1].Trim();
            AppendNewValue(key, value);
        }

        EndStanza();

        return new DebianControlFile(stanzas);
    }

    public List<Stanza> Stanzas = stanzas;
}
