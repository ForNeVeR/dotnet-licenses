module DotNetLicenses.Ignores

open Microsoft.Extensions.FileSystemGlobbing
open TruePath

let Licenses = Preset "licenses"

let DefaultPresets: IgnorePattern[] = [|
    Licenses
|]

let AllPresets = Map.ofArray [|
    "licenses", LocalPathPattern "LICENSES/*.txt"
|]

let Filter(patterns: IgnorePattern seq) (items: ISourceEntry seq): ISourceEntry seq =
    let getFilter = function
        | Preset name -> Map.find name AllPresets

    let itemsByPath =
        items
        |> Seq.groupBy _.SourceRelativePath

    let patterns = patterns |> Seq.map getFilter
    let matcher = Matcher()
    matcher.AddIncludePatterns(patterns |> Seq.map _.Value)

    itemsByPath
    |> Seq.filter(fun (path, _) -> not (matcher.Match(path).HasMatches))
    |> Seq.collect snd
