module HttpCache

open System
open System.IO
open System.Collections.Generic
open Titanium.Web.Proxy.Http

type Mode = Precise | StripArgValues | StripArgNamesValues

type ICache =
    abstract member Keys: unit -> (string * string)[]
    abstract member Get: string -> string -> option<Response>
    abstract member Put: string -> string -> Response -> bool
    abstract member Clear: unit -> unit

type Mem() =
    let v = Dictionary<string * string, Response>()

    interface ICache with
        member this.Get method url =
            match v.TryGetValue((method, url)) with
            | true, v -> Some v
            | _ -> None
        member this.Put method url resp =
            v[(method, url)] <- resp
            true
        member this.Clear() = v.Clear()
        member this.Keys() = v.Keys |> Seq.toArray

let uriNoArg (u:string) =
    Uri u |> Url.Args.map (fun n _ -> n, "_") |> string

let uriNoArgVal (u:string) =
    Uri u |> Url.Args.map (fun _ _ -> "_", "_") |> Url.Fragment.drop |> string

type UrlMap(urlMap: string->string, c:ICache) =
    let mutable map = c.Keys() |> Array.map (fun (m, u) -> urlMap u, u) |> Map.ofArray

    interface ICache with
        member this.Get method url = Map.tryFind (urlMap url) map |> Option.bind (fun u -> c.Get method u)
        member this.Put method url resp =
            if c.Put method url resp then
                map <- Map.add (urlMap url) url map
                true
            else false
        member this.Clear() = c.Clear()
        member this.Keys() = c.Keys()

type Lock(c:ICache) =
    let lockObj = obj()

    interface ICache with
        member this.Get method url = lock lockObj (fun () -> c.Get method url)
        member this.Put method url resp = lock lockObj (fun () -> c.Put method url resp)
        member this.Clear() = lock lockObj c.Clear
        member this.Keys() = lock lockObj c.Keys

type All(caches:ICache[]) =
    interface ICache with
        member this.Get method url = caches |> Array.tryPick (fun c -> c.Get method url)
        member this.Put method url resp = caches |> Array.fold (fun st c -> c.Put method url resp || st) false
        member this.Clear() = caches |> Array.iter (fun c -> c.Clear())
        member this.Keys() = caches |> Array.collect (fun c -> c.Keys())

type ReadOnlyNCache(n: int, c:ICache) =
    let v = Dictionary<string * string, Response>()

    interface ICache with
        member this.Get method url =
            match v.TryGetValue((method, url)) with
            | true, v -> Some v
            | _ ->
                match c.Get method url with
                | None -> None
                | Some resp as r ->
                    if v.Count >= n then
                        v.Remove (v.Keys |> Seq.head) |> ignore
                    v[(method, url)] <- resp
                    r
        member this.Put _ _ _ = false
        member this.Clear() = v.Clear()
        member this.Keys() = c.Keys()

type Warc(rd:StreamReader) =
    let map =
        Warc.readPairs rd
        |> Seq.map (fun (req, resp) -> (Http.getMethod req.Body, req.Url), (resp.BodyStart, resp.BodyEnd - resp.BodyStart))
        |> Map.ofSeq

    interface ICache with
        member this.Get method url =
            Map.tryFind (method, url) map
            |> Option.map (fun (offset, len) -> IO.readChunk rd.BaseStream offset len |> Http.toResponse)
        member this.Put _ _ _ = false
        member this.Clear() = ()
        member this.Keys() = Seq.toArray map.Keys

type Log(c:ICache) =
    interface ICache with
        member this.Get method url =
            eprintfn "Cache get: %s %s" method url
            c.Get method url
        member this.Put method url resp =
            eprintfn "Cache put: %s %s" method url
            c.Put method url resp
        member this.Clear() =
            eprintfn "Cache clear"
            c.Clear()
        member this.Keys() =
            let r = c.Keys()
            eprintfn "Cache keys: %A" r
            r


type Recent(c:ICache) =
    let m = Lock(Mem()) :> ICache
    member this.Recent = m
    member this.Cache = this :> ICache

    interface ICache with
        member this.Get method url =
            match c.Get method url with
            | None -> None
            | Some resp as r ->
                m.Put method url resp |> ignore
                r

        member this.Put method url resp =
            m.Put method url resp |> ignore
            c.Put method url resp

        member this.Clear() =
            c.Clear()

        member this.Keys() = c.Keys()

let empty mode files =
    let warcs = ReadOnlyNCache(50, All([| for i in files -> Warc i |]))
    let main = All([|
        Mem()
        warcs
        if mode <> Precise then
            UrlMap(uriNoArg, Mem())
            if Array.length files > 0 then
                UrlMap(uriNoArg, warcs)
        if mode = StripArgNamesValues then
            UrlMap(uriNoArgVal, Mem())
            if Array.length files > 0 then
                UrlMap(uriNoArgVal, warcs)
    |])
    Recent(Lock(main))
