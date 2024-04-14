// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DebianControlFileSpec;

public class Stanza
{
    public List<(string, string)> Fields { get; } = new();
}
