// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.ZipFiles

open System.IO
open System.IO.Compression
open TruePath

let SingleFileArchive(archivePath: AbsolutePath, fileRelativePath: string, fileContent: byte[]): unit =
    use stream = File.OpenWrite archivePath.Value
    use archive = new ZipArchive(stream, ZipArchiveMode.Create)
    use entry = archive.CreateEntry(fileRelativePath).Open()
    entry.Write(fileContent, 0, fileContent.Length)
