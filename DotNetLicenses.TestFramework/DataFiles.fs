// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.DataFiles

open System.IO
open System.Reflection
open System.Threading.Tasks
open TruePath

let Get(name: string): AbsolutePath =
    let currentAssemblyPath = Assembly.GetExecutingAssembly().Location
    let filePath = Path.Combine(Path.GetDirectoryName(currentAssemblyPath), "Data", name)
    if not (File.Exists filePath) then
        failwithf $"Data file \"{name}\" not found."
    AbsolutePath(Path.GetFullPath filePath)

let private CopyDataFiles(paths: AbsolutePath[]) =
    let tempDir = AbsolutePath(Path.GetTempFileName())
    File.Delete tempDir.Value
    Directory.CreateDirectory tempDir.Value |> ignore
    for path in paths do
        let tempPath = tempDir / path.FileName
        File.Copy(path.Value, tempPath.Value)
    tempDir

let Deploy(name: string) (action: AbsolutePath -> Task): Task = task {
    let path = CopyDataFiles [| Get name |]
    try
        do! action <| path / name
    finally
        Directory.Delete(path.Value, true)
}

let DeployGroup(names: string[]) (action: AbsolutePath -> Task): Task = task {
    let path = CopyDataFiles(names |> Array.map Get)
    try
        do! action <| path
    finally
        Directory.Delete(path.Value, true)
}
