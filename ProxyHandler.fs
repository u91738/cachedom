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

let onRequest verbose cache (e: SessionEventArgs) =
    task {
        try
            if blockHosts |> Array.exists e.HttpClient.Request.RequestUri.Host.Contains then
                e.Respond notFound
            else
                match HttpCache.get cache e.HttpClient.Request.Method e.HttpClient.Request.Url with
                | Some resp ->
                    if verbose then
                        printfn "Cached response to %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url
                    e.Respond resp
                | None ->
                    if verbose then
                        printfn "Pass to network %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url
        with e -> eprintfn "onRequest exception\n%A" e
    }

let onResponse cache (e: SessionEventArgs) =
    task {
        try
            e.HttpClient.Response.KeepBody <- true
            if e.HttpClient.Response.HasBody then
                let! _ = e.GetResponseBody()
                ()
            HttpCache.add cache e.HttpClient.Request.Method e.HttpClient.Request.Url e.HttpClient.Response
        with e -> eprintfn "onResponse exception\n%A" e
    }
