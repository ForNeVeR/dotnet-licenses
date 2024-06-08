module DotNetLicenses.GeneratedArtifacts

open System.Collections.Generic
open TruePath

[<RequireQualifiedAccess>]
type GeneratedArtifact =
    | File of AbsolutePath
    | Pattern of LocalPathPattern

type GeneratedArtifactEntry = {
    Artifact: GeneratedArtifact
    AdditionalLicense: ILicense option
}

let Merge(items: GeneratedArtifactEntry seq): GeneratedArtifactEntry seq =
    items
    |> Seq.groupBy _.Artifact
    |> Seq.map (fun (k, v) ->
        let license = v |> Seq.choose _.AdditionalLicense |> Seq.toArray |> ILicense.Merge
        {
            Artifact = k
            AdditionalLicense = license
        }
    )

let Split(entries: GeneratedArtifactEntry seq): IList<AbsolutePath> * IList<LocalPathPattern> =
    let files = ResizeArray()
    let patterns = ResizeArray()
    for entry in entries do
        match entry.Artifact with
        | GeneratedArtifact.File f -> files.Add f
        | GeneratedArtifact.Pattern p -> patterns.Add p

    files, patterns
