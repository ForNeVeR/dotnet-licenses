// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.ZipFiles

open System.IO
open System.IO.Compression

let SingleFileArchive(archivePath: string, fileRelativePath: string, fileContent: byte[]): unit =
    use stream = File.OpenWrite archivePath
    use archive = new ZipArchive(stream, ZipArchiveMode.Create)
    use entry = archive.CreateEntry(fileRelativePath).Open()
    entry.Write(fileContent, 0, fileContent.Length)
