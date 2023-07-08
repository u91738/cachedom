module Http

open System
open System.Text
open Titanium.Web.Proxy.Http


let code c (s:string) =
    let r = Response(Encoding.UTF8.GetBytes s)
    r.KeepBody <- true
    r.HttpVersion <- Version("1.1")
    r.ContentType <- "text/html"
    r.StatusCode <- c
    r

let notFound = code 404 "404 Not found"
let ok = code 200 "200 OK"

let getMethod (req: string) =
        match req.Split(' ', 2) with
        | [| m; _ |] -> m
        | _ -> "GET"

let toResponse (http: string) =
    let statusLine, hb = match http.Split('\n', 2) with
                         | [| st; hb |] -> st, hb
                         | _ -> failwithf "Invalid HTTP: %A" http

    let httpVer, status, desc = match statusLine.Split(' ', 3) with
                                | [| httpVer; code; desc |] ->
                                    match Int32.TryParse code with
                                    | true, c ->
                                        let v = match httpVer.Split('/') with
                                                | [| "HTTP"; vtext|] -> Version vtext
                                                | _ -> Version "HTTP/1.1"
                                        v, c, desc
                                    | _ -> failwithf "Invalid HTTP status: %A" http
                                | _ -> failwithf "Invalid HTTP status line: %A" http

    let headers, body = match hb.IndexOf "\n\r\n" with // RFC 7230 says CR LF
                        | -1 -> hb.Split '\n', ""
                        | delim -> hb.Substring(0, delim).Split('\n', StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries),
                                   hb.Substring(delim).TrimStart()
    let r = Response(Encoding.UTF8.GetBytes body) // other encodings are not worth the effort
    r.KeepBody <- true
    r.HttpVersion <- httpVer
    r.StatusCode <- status
    r.StatusDescription <- desc
    for h in headers do
        match h.Split(':', 2, StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries) with
        | [| "Content-Length" ; _ |]
        | [||] -> ()
        | [| k; v |] -> r.Headers.AddHeader(k, v)
        | [| k |] -> r.Headers.AddHeader(k, "")
        | err -> failwithf "Impossible %A" err
    r

let withBody (resp:Response) body =
    let r = Response(body)
    r.StatusCode <- resp.StatusCode
    r.StatusDescription <- resp.StatusDescription
    r.ContentType <- resp.ContentType
    r.HttpVersion <- resp.HttpVersion
    r.KeepBody <- true
    for h in resp.Headers do
        if h.Name <> "Content-Length" && h.Name <> "Content-Type" then
            r.Headers.AddHeader h
    r
