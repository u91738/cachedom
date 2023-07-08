module Warc

open System
open System.IO

type Record = {
    Url: string
    Type: string
    BodyStart: int
    BodyEnd: int
    Body: string
}

let private parseRecord lines =
    let headerLines = lines |> Array.takeWhile (fun (_, i) -> i <> "")
    let headers = headerLines
                  |> Array.map (fun (_, i) ->
                                    match i.Split(':', 2, StringSplitOptions.TrimEntries) with
                                    | [| k; v |] -> k, v
                                    | _ -> failwithf "Invalid WARC field: %s" i)
                  |> Map.ofArray
    match Map.tryFind "WARC-Type" headers, Map.tryFind "WARC-Target-URI" headers with
    | Some recordType, Some url when headerLines.Length < lines.Length ->
        let body = lines |> Array.skip (headerLines.Length + 1)
        let bodyLastLineOffset, bodyLastLine = body[body.Length - 1]
        Some {
            Url = url.Substring(1, url.Length - 2)
            Type = recordType
            BodyStart = fst body[0]
            BodyEnd = bodyLastLineOffset + bodyLastLine.Length
            Body = String.Join('\n', body |> Array.map snd)
        }
    | _ -> None

let split v s = seq {
    let r = ResizeArray()
    for i in s do
        if snd i = v then
            yield r.ToArray()
            r.Clear()
        else
            r.Add i
    if r.Count > 0 then
        yield r.ToArray()
}

let read (f:StreamReader) =
    let lines = f |> IO.readLines |> split "WARC/1.0"
    lines
    |> Seq.filter (Array.isEmpty >> not)
    |> Seq.choose parseRecord

let readPairs f =
    read f
    |> Seq.pairwise
    |> Seq.filter (fun (a, b) -> a.Url = b.Url && a.Type = "request" && b.Type = "response")
