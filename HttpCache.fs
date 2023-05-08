module HttpCache

open System
open System.Collections.Generic
open Titanium.Web.Proxy.Http

type Mode = Precise | StripArgValues | StripArgNamesValues

type HttpCache = private {
    Lock: obj
    Mode: Mode
    Precise: Dictionary<string * string, Response>
    NoArgValues: Dictionary<string * string, Response>
    NoArgNamesValues: Dictionary<string * string, Response>
}

let empty mode = {
    Lock = obj()
    Mode = mode
    Precise = Dictionary()
    NoArgValues = Dictionary()
    NoArgNamesValues = Dictionary()
}

let private uriNoArg (u:string) replaceNames =
    if replaceNames then
        Uri u |> Url.Args.map (fun _ _ -> "_", "_") |> Url.Fragment.drop |> string
    else
        Uri u |> Url.Args.map (fun n _ -> n, "_") |> string

let add cache method url resp =
    lock cache.Lock (fun () ->
        cache.Precise[(method, url)] <- resp
        if cache.Mode = StripArgValues || cache.Mode = StripArgNamesValues then
            cache.NoArgValues[(method, uriNoArg url false)] <- resp
        if cache.Mode = StripArgNamesValues then
            cache.NoArgNamesValues[(method, uriNoArg url true)] <- resp
    )

let get cache method url =
    lock cache.Lock (fun () ->
        match cache.Precise.TryGetValue ((method, url)) with
        | true, r -> Some r
        | _ ->
            match cache.NoArgValues.TryGetValue ((method, uriNoArg url false)) with
            | true, r -> Some r
            | _ ->
                match cache.NoArgNamesValues.TryGetValue ((method, uriNoArg url true)) with
                | true, r -> Some r
                | _ -> None
    )

let getAll cache =
    lock cache.Lock (fun () -> Array.ofSeq cache.Precise)

let clear cache =
    lock cache.Lock (fun () ->
        cache.Precise.Clear()
        cache.NoArgValues.Clear()
        cache.NoArgNamesValues.Clear()
    )