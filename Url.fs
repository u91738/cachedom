module Url

open System
open System.Linq

module Fragment=

    let set (u:Uri) v =
        let r = UriBuilder(u)
        r.Fragment <- v
        r.Uri

    let drop (u:Uri) = set u ""

module Args=

    let toTuple (s:string) =
        match s.Split('=', 2) with
        | [| k; v |] -> k, v
        | [| k |] -> k, ""
        | _ -> failwith "Split can't do this!"

    let fromTuple (k, v) =
        if String.length v > 0 then
            sprintf "%s=%s" k v
        else
            k

    let join (args:string[]) =
        String.Join('&', args)

    let get (u:Uri) =
        if u.Query.Length = 0 then
            [| |]
        else
            u.Query.Substring(1).Split '&' |> Array.map toTuple

    let map (f:string->string->string*string) (u:Uri) =
        let r = new UriBuilder(u)
        r.Query <- get u |> Array.map (fun (k, v) -> f k v |> fromTuple) |> join
        r.Uri

    let mapNth (u:Uri) index f =
        let r = new UriBuilder(u)
        let args = get u
                   |> Array.mapi (fun i (k, v) ->
                                    if i = index then
                                        fromTuple (f k v)
                                    else
                                        fromTuple (k, v))

        r.Query <- join args
        r.Uri

    let setVal newVal k _ = k, Uri.EscapeDataString newVal

    let setName newVal _ v = Uri.EscapeDataString newVal, v

    let count (u:Uri) =
        if u.Query.Length > 0 then
            1 + u.Query.Count(fun i -> i = '&')
        else
            0
