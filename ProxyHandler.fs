module ProxyHandler

open Titanium.Web.Proxy.EventArguments

let private blockHosts = [|
    "accounts.google.com"
    ".googleapis.com"
    "chrome-devtools-frontend.appspot.com"
    ".gvt1.com"
|]

let onRequest verbose (cache:HttpCache.ICache) instr (e: SessionEventArgs) =
    task {
        try
            if blockHosts |> Array.exists e.HttpClient.Request.RequestUri.Host.Contains then
                e.Respond Http.notFound
            else
                match cache.Get e.HttpClient.Request.Method e.HttpClient.Request.Url with
                | Some resp ->
                    if verbose then
                        printfn "Cached response to %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url

                    match JsInstrumentation.Sync.add instr resp.ContentType resp.BodyString with
                    | Some instrBody ->
                        instrBody |> resp.Encoding.GetBytes |> Http.withBody resp |> e.Respond
                    | None -> e.Respond resp
                | None ->
                    if verbose then
                        printfn "Pass to network %s %s" e.HttpClient.Request.Method e.HttpClient.Request.Url
        with e -> eprintfn "onRequest exception\n%A" e
    }

let onResponse (cache:HttpCache.ICache) instr (e: SessionEventArgs) =
    task {
        try
            e.HttpClient.Response.KeepBody <- true
            if e.HttpClient.Response.HasBody then
                let! _ = e.GetResponseBody()
                ()
            cache.Put e.HttpClient.Request.Method e.HttpClient.Request.Url e.HttpClient.Response |> ignore

            JsInstrumentation.Sync.add instr e.HttpClient.Response.ContentType e.HttpClient.Response.BodyString
            |> Option.iter e.SetResponseBodyString
        with e -> eprintfn "onResponse exception\n%A" e
    }
