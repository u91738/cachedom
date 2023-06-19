module ProxyHandler

open Titanium.Web.Proxy.Http
open Titanium.Web.Proxy.EventArguments

let private notFound =
    let r = Response()
    r.StatusCode <- 404
    r

let private blockHosts = [|
    "accounts.google.com"
    ".googleapis.com"
    "chrome-devtools-frontend.appspot.com"
    ".gvt1.com"
|]

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

let onRequest verbose cache instr (e: SessionEventArgs) =
    task {
        try
            if blockHosts |> Array.exists e.HttpClient.Request.RequestUri.Host.Contains then
                e.Respond notFound
            else
                match HttpCache.get cache e.HttpClient.Request.Method e.HttpClient.Request.Url with
                | Some resp ->
                    if verbose then
                        printfn "Cached response to %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url

                    match JsInstrumentation.Sync.add instr resp.ContentType resp.BodyString with
                    | Some instrBody ->
                        instrBody |> resp.Encoding.GetBytes |> withBody resp |> e.Respond
                    | None -> e.Respond resp
                | None ->
                    if verbose then
                        printfn "Pass to network %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url
        with e -> eprintfn "onRequest exception\n%A" e
    }

let onResponse cache instr (e: SessionEventArgs) =
    task {
        try
            e.HttpClient.Response.KeepBody <- true
            if e.HttpClient.Response.HasBody then
                let! _ = e.GetResponseBody()
                ()
            HttpCache.add cache e.HttpClient.Request.Method e.HttpClient.Request.Url e.HttpClient.Response

            JsInstrumentation.Sync.add instr e.HttpClient.Response.ContentType e.HttpClient.Response.BodyString
            |> Option.iter e.SetResponseBodyString
        with e -> eprintfn "onResponse exception\n%A" e
    }
