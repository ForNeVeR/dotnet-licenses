namespace DotNetLicenses

open System.Collections.Concurrent
open System.Threading.Tasks
open TruePath

type MsBuildItemCache<'TItem>() =

    let cache = ConcurrentDictionary<AbsolutePath, Task<'TItem[]>>()

    member _.Get (input: AbsolutePath) (loader: AbsolutePath -> Task<'TItem[]>): Task<'TItem[]> =
        match cache.TryGetValue input with
        | true, result -> result
        | false, _ ->
            let task = loader input
            cache.TryAdd(input, task) |> ignore
            task
