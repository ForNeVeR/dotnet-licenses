// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.DataFiles

open System.IO
open System.Reflection
open System.Threading.Tasks

let Get(name: string): string =
    let currentAssemblyPath = Assembly.GetExecutingAssembly().Location
    let filePath = Path.Combine(Path.GetDirectoryName(currentAssemblyPath), "Data", name)
    if not (File.Exists filePath) then
        failwithf $"Data file \"{name}\" not found."
    Path.GetFullPath filePath

let private CopyDataFile(path: string) =
    let tempDir = Path.GetTempFileName()
    File.Delete tempDir
    Directory.CreateDirectory tempDir |> ignore
    let tempPath = Path.Combine(tempDir, Path.GetFileName path)
    File.Copy(path, tempPath)
    tempPath

let Deploy(name: string) (action: string -> Task): Task = task {
    let path = CopyDataFile name
    try
        do! action path
    finally
        File.Delete path
}
