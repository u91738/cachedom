open System
open Config
open Check

let private help ="""cachedom - search for DOM XSS with limited interaction with server
Usage:
    cachedom --url http://example.com?a=123\n\
OPTIONS
(See default.json for defaults)
-u URL, --url URL
    url to check, pass multiple --url ... if needed
-c configFile, --config configFile
    path to config file. See default.json
-s, --check-sub-urls
    check urls used in subrequests of what was passed in --url ... (AJAX, frames)
-p PORT, --proxy-port PORT
    port to use for internal proxy server
--js-body-filter, --no-js-body-filter
    only check pages if they have on...=, javascript, <script etc in their body
--show-browser
    do not run browser in headless mode, mostly a debug feature
--cache-mode [precise|strip-arg-values|strip-arg-names-values]
    precise
        respond from cache to requests that fully match previous request in cache
    strip-arg-values
        respond from cache to requests that have different arg value
        i.e. reply with response from http://example.com/some/page?a=123
        to request for http://example.com/some/page?a=abc
    strip-arg-names-values
        respond from cache to requests that have different arg name and value
        i.e. reply with response from http://example.com/some/page?a=123
        to request for http://example.com/some/page?x=abc"""

let rec private parseArgs args urls conf =
    match args with
    | "-h" :: _ | "-?" :: _ | "--help" :: _ ->
        eprintfn "%s" help
        None
    | "-c" :: configFile :: tail
    | "--config" :: configFile :: tail ->
        parseArgs tail urls (Config.read configFile)
    | "-s" :: tail
    | "--check-sub-urls" :: tail ->
        parseArgs tail urls {conf with CheckSubUrls = true}
    | "-u" :: u :: tail
    | "--url" :: u :: tail ->
        parseArgs tail (Uri u :: urls) conf
    | "-p" :: port :: tail
    | "--proxy-port" :: port :: tail ->
        parseArgs tail urls {conf with ProxyPort = int (UInt16.Parse port) }
    | "--show-browser" :: tail ->
        parseArgs tail urls {conf with ShowBrowser = true }
    | "--cache-mode" :: "precise" :: tail ->
        parseArgs tail urls {conf with CacheMode = HttpCache.Mode.Precise }
    | "--cache-mode" :: "strip-arg-values" :: tail ->
        parseArgs tail urls {conf with CacheMode = HttpCache.Mode.StripArgValues }
    | "--cache-mode" :: "strip-arg-names-values" :: tail ->
        parseArgs tail urls {conf with CacheMode = HttpCache.Mode.StripArgNamesValues }
    | "--js-body-filter" :: tail ->
        parseArgs tail urls {conf with JsBodyFilter = true }
    | "--no-js-body-filter" :: tail ->
        parseArgs tail urls {conf with JsBodyFilter = false }
    | [] -> Some (urls, conf)
    | _ -> failwith "Failed to parse arguments"

let private typeDesc t =
    match t with
    | Url.Inputs.Fragment -> "Url #fragment"
    | Url.Inputs.Arg arg -> sprintf "GET parameter %d" arg

let private withColor c f =
    let old = Console.ForegroundColor
    Console.ForegroundColor <- c
    f()
    Console.ForegroundColor <- c

let private urlReport input res =
    match res with
    | Exec value ->
        printfn "%s leads to JS execution in Url:" (typeDesc input)
        withColor ConsoleColor.Yellow (fun () -> printfn "%s" (string value))
    | Refl refl ->
        for k, v in Array.groupBy snd refl do
            printfn "%A charsets:" k
            let charsets = v |> Array.map fst
            let cstext = [|
                if Array.contains Lower charsets then
                    yield "Lower"
                if Array.contains Upper charsets then
                    yield "Upper"
                if Array.contains Numeric charsets then
                    yield "Numeric"

                let special = new String(Array.choose (function | Special c -> Some c | _ -> None) charsets)
                if special.Length > 0 then
                    yield sprintf "\"%s\"" special
            |]

            printfn "%s" (String.Join(", ", cstext))

[<EntryPoint>]
let main argv =
    match parseArgs (Array.toList argv) [] Config.Default with
    | None -> 1
    | Some (urls, conf) ->
        let cache = HttpCache.empty conf.CacheMode
        use _ = Proxy.start (ProxyHandler.onRequest false cache) (ProxyHandler.onResponse cache) conf.ProxyPort
        let selproxy = Proxy.selenium "localhost" conf.ProxyPort
        use browser = Browser.get selproxy (not conf.ShowBrowser) conf.UserAgent
        let ctx = {
            Browser = browser
            Cache = cache
            Payloads = conf.Payloads
            WaitAfterNavigation = conf.WaitAfterNavigation
        }
        let filterMode = if conf.JsBodyFilter then Filter else Brute

        printfn "------------------------------------------------------------"
        for url in urls do
            if conf.CheckSubUrls then
                HttpCache.clear cache
                Browser.navigate browser false url conf.WaitAfterNavigation
                for cached in HttpCache.getAll cache do
                    let method, cachedUrl = cached.Key
                    if method = "GET" && cached.Value.HeaderText.Contains("text/html", StringComparison.InvariantCultureIgnoreCase) then
                        printfn "Check url: %s" (string cachedUrl)
                        for input, res in Check.url ctx filterMode (Uri cachedUrl) do
                            urlReport input res
            else
                printfn "Check url: %s" (string url)
                for input, res in Check.url ctx filterMode url do
                    urlReport input res
        0
