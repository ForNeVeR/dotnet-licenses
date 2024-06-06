module DotNetLicenses.GeneratedArtifacts

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
