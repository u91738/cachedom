﻿open System
open System.IO
open Config
open Check

let private help ="""cachedom - search for DOM XSS with limited interaction with server
Usage:
    cachedom --url http://example.com?a=123\n\
OPTIONS
(See default.json for defaults)
-u URL, --url URL
    url to check, pass multiple --url ... if needed
-w WARCFILE, --warc WARCFILE
    path to a warc file to be used as source of server responses, pass multiple --warc ... if needed
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
--ignore-cookies
    do not try to put payloads into cookies. Can save a lot of time.
--cache-fail [not-found|ok|network]
    if cache has no response reply with
    not-found
        HTTP 404, do not allow requests to the real network
    ok
        HTTP 200, do not allow requests to the real network
    network
        pass request to network
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

let rec private parseArgs args urls warc conf =
    match args with
    | "-h" :: _ | "-?" :: _ | "--help" :: _ ->
        eprintfn "%s" help
        None
    | "-c" :: configFile :: tail
    | "--config" :: configFile :: tail ->
        parseArgs tail urls warc (Config.read configFile)
    | "-s" :: tail
    | "--check-sub-urls" :: tail ->
        parseArgs tail urls warc {conf with CheckSubUrls = true}
    | "-u" :: u :: tail
    | "--url" :: u :: tail ->
        parseArgs tail (u :: urls) warc conf
    | "-p" :: port :: tail
    | "--proxy-port" :: port :: tail ->
        parseArgs tail urls warc {conf with ProxyPort = int (UInt16.Parse port) }
    | "--show-browser" :: tail ->
        parseArgs tail urls warc {conf with ShowBrowser = true }
    | "--cache-mode" :: "precise" :: tail ->
        parseArgs tail urls warc {conf with CacheMode = HttpCache.Mode.Precise }
    | "--cache-mode" :: "strip-arg-values" :: tail ->
        parseArgs tail urls warc {conf with CacheMode = HttpCache.Mode.StripArgValues }
    | "--cache-mode" :: "strip-arg-names-values" :: tail ->
        parseArgs tail urls warc {conf with CacheMode = HttpCache.Mode.StripArgNamesValues }
    | "--cache-fail" :: "not-found" :: tail ->
        parseArgs tail urls warc {conf with CacheFailMode = ProxyHandler.NoCache.NotFound }
    | "--cache-fail" :: "ok" :: tail ->
        parseArgs tail urls warc {conf with CacheFailMode = ProxyHandler.NoCache.Ok }
    | "--cache-fail" :: "network" :: tail ->
        parseArgs tail urls warc {conf with CacheFailMode = ProxyHandler.NoCache.PassToNetwork }
    | "--js-body-filter" :: tail ->
        parseArgs tail urls warc {conf with JsBodyFilter = true }
    | "--no-js-body-filter" :: tail ->
        parseArgs tail urls warc {conf with JsBodyFilter = false }
    | "--ignore-cookies" :: tail ->
        parseArgs tail urls warc {conf with IgnoreCookies = true }
    | "-w" :: fname :: tail
    | "--warc" :: fname :: tail ->
        parseArgs tail urls (fname :: warc) conf
    | [] -> Some (urls, warc, conf)
    | _ -> failwith "Failed to parse arguments"

let private typeDesc t =
    match t with
    | Browser.Inputs.Fragment -> "Url #fragment"
    | Browser.Inputs.Arg arg -> sprintf "GET parameter %d" arg
    | Browser.Inputs.Cookie c -> sprintf "Cookie %s = %s" c.Name c.Value

let private withColor c f =
    let old = Console.ForegroundColor
    Console.ForegroundColor <- c
    f()
    Console.ForegroundColor <- c

let private charsetToString = function
    | Special c -> sprintf "\"%c\"" c
    | cs -> string cs

let private urlReport input res =
    match res with
    | Exec value ->
        printfn "%s leads to JS execution in Url:" (typeDesc input)
        withColor ConsoleColor.Yellow (fun () -> printfn "%s" (string value))
    | Refl refl ->
        let getReflKey (_, _, r) =
            match r with
            | Body -> "Body"
            | Log -> "Log"
            | Cookie (name, _) -> sprintf "Cookie %s" name
            | Instr (name, _)  -> sprintf "Instr %s" name

        for k, v in Array.groupBy getReflKey refl do
            printfn "%s:" k

            for url, cs, r in v do
                match r with
                | Log | Body -> printfn "    %s %A\n" (charsetToString cs) url
                | Cookie (name, value) -> printfn "    %s %A ( \"%s\" = \"%s\" )\n" (charsetToString cs) url name value
                | Instr (name, ctx) ->
                    let stack = String.Join('\n', ctx.Stack.Split('\n') |> Array.map (fun i -> "    " + i))
                    let args = String.Join(',', ctx.Args)
                    printfn "    %s %A" (charsetToString cs) url
                    printfn "    call: %s ( %s )" name args
                    printfn "exception stack:\n%s\n" stack

let checkUrls ctx urls =
    for url in urls do
        printfn "Check url: %s" (string url)
        for input, res in Check.url ctx (Uri url) do
            ctx.Cache.Recent.Clear()
            urlReport input res

let rec checkUrlsRec ctx urls checkedUrls =
    match urls with
    | [] -> ()
    | url::rest when Set.contains url checkedUrls -> checkUrlsRec ctx rest checkedUrls
    | url::rest ->
        printfn "Check url: %s" (string url)
        let recents = [|
            for input, res in Check.url ctx (Uri url) do
                ctx.Cache.Recent.Clear()
                urlReport input res
                yield! ctx.Cache.Recent.Keys()
        |]
        let newUrls = recents |> Array.map snd |> Array.toList
        checkUrlsRec ctx (newUrls @ rest) (Set.add url checkedUrls)

[<EntryPoint>]
let main argv =
    match parseArgs (Array.toList argv) [] [] Config.Default with
    | None -> 1
    | Some (urls, warcFilenames, conf) ->
        use warcFiles = Disposable.composite [| for i in warcFilenames -> new StreamReader(File.OpenRead i)|]
        let cache = HttpCache.empty conf.CacheMode warcFiles.Data
        let instr = JsInstrumentation.Sync.create false
        use _ = Proxy.start
                    (ProxyHandler.onRequest false conf.CacheFailMode cache instr)
                    (ProxyHandler.onResponse cache instr)
                    conf.ProxyPort
        let selproxy = Proxy.selenium "localhost" conf.ProxyPort
        use browser = Browser.get selproxy (not conf.ShowBrowser) conf.UserAgent
        let ctx = {
            Browser = browser
            Cache = cache
            JsInstr = instr
            Payloads = conf.Payloads
            WaitAfterNavigation = conf.WaitAfterNavigation
            FilterMode = if conf.JsBodyFilter then Filter else Brute
            InputKinds = [
                Browser.Inputs.Kind.Arg
                Browser.Inputs.Kind.Fragment
                if not conf.IgnoreCookies then
                    Browser.Inputs.Kind.Cookie
            ]
        }

        printfn "------------------------------------------------------------"
        if conf.CheckSubUrls then
            checkUrlsRec ctx urls Set.empty
        else
            checkUrls ctx urls

        0
