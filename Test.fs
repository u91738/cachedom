module Test

open System
open System.IO
open System.Diagnostics
open FSharp.Data.LiteralProviders
open Xunit
open Swensen.Unquote

open Check

type TestProxy(useWarc:bool) =
    let conf = if useWarc then
                  { Config.Default with
                      ProxyPort = Config.Default.ProxyPort + 1
                      CacheFailMode = ProxyHandler.NoCache.Ok
                  }
               else Config.Default

    let warcFiles = if useWarc then
                        [| new StreamReader(File.OpenRead "../../../test/test.warc") |]
                    else
                        [| |]
    let cache = HttpCache.empty conf.CacheMode warcFiles
    let instr = JsInstrumentation.Sync.create false
    let proxy = Proxy.start (ProxyHandler.onRequest false conf.CacheFailMode cache instr) (ProxyHandler.onResponse cache instr) conf.ProxyPort
    let selproxy = Proxy.selenium "localhost" conf.ProxyPort
    let browser = Browser.get selproxy (not conf.ShowBrowser) conf.UserAgent

    member this.Ctx = {
        Browser = browser
        Cache = cache
        JsInstr = instr
        Payloads = conf.Payloads
        WaitAfterNavigation = 0
        FilterMode = Filter
        InputKinds = [ Browser.Inputs.Kind.Arg; Browser.Inputs.Kind.Fragment ]
    }

    with
        interface IDisposable with
            member _.Dispose() =
                browser.Dispose()
                proxy.Dispose()
                for i in warcFiles do
                    i.Dispose()

type BrowserFixture() =
    let proxy = new TestProxy false
    let warcProxy = new TestProxy true
    member this.Ctx useWarc = (if useWarc then warcProxy else proxy).Ctx
    with
        interface IDisposable with
            member _.Dispose() =
                (proxy:> IDisposable).Dispose()
                (warcProxy :> IDisposable).Dispose()

type ServerFixture() =
    let proc = Process.Start("sh", "../../../test/server.sh")
    with
        interface IDisposable with
            member _.Dispose() =
                proc.Dispose()

let isExec = function | _, Exec _ -> true | _, _ -> false

let execOnly r = Array.length r > 0 && Array.forall isExec r

let reflOnly r = Array.length r > 0 && Array.forall (isExec >> not) r

let hasFragExec (r: (Browser.Inputs.Input*_)[]) =
    r |> Array.exists (function
                       | i, Exec (_) when i.IsFragment -> true
                       | _ -> false)

let hasArgExec (r: (Browser.Inputs.Input*_)[]) =
    r |> Array.exists (function
                     | i, Exec (_) when i.IsArg -> true
                     | _ -> false)

let hasCharset r (cs, t) =
    Array.exists (function
                  | _, Refl refl -> Array.exists (fun (_, rcs, rt) -> rcs = cs && rt = t) refl
                  | _ -> false) r

let noCharset r cs = not (hasCharset r cs)

let isInstr = function | Instr _ -> true | _ -> false

let hasInstrCharset r cs =
    Array.exists (function
                  | _, Refl refl -> Array.exists (fun (_, rcs, rt) -> rcs = cs && isInstr rt) refl
                  | _ -> false) r

let noInstrCharset r cs = not (hasInstrCharset r cs)

let isCookie = function | Cookie _ -> true | _ -> false

let hasCookieCharset r cs =
    Array.exists (function
                  | _, Refl refl -> Array.exists (fun (_, rcs, rt) -> rcs = cs && isCookie rt) refl
                  | _ -> false) r

type Tests(ctx:BrowserFixture, srv:ServerFixture) =
    interface IClassFixture<BrowserFixture>
    interface IClassFixture<ServerFixture>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Param write`` useWarc =
        let url = Uri $"http://testhost:8000/arg-write.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasArgExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Angular frag write`` useWarc =
        let url = Uri $"http://testhost:8000/angular.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Vue2 frag`` useWarc =
        let url = Uri $"http://testhost:8000/vue2.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Vue3 components`` useWarc =
        let url = Uri $"http://testhost:8000/vue3-comp.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Vue2 components`` useWarc =
        let url = Uri $"http://testhost:8000/vue2-comp.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Vue3 frag`` useWarc =
        let url = Uri $"http://testhost:8000/vue3.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Fragment eval`` useWarc =
        let url = Uri $"http://testhost:8000/frag-eval.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Fragment setTimeout`` useWarc =
        let url = Uri $"http://testhost:8000/frag-timeout.html?a=123"
        let c = { ctx.Ctx useWarc with WaitAfterNavigation = Config.Default.WaitAfterNavigation }
        let r = Check.url c url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Param write alnum`` useWarc =
        let url = Uri $"http://testhost:8000/arg-write-alnum.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Upper, Body) @>
        test <@ hasCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>

        test <@ hasInstrCharset r Lower @>
        test <@ hasInstrCharset r Upper @>
        test <@ hasInstrCharset r Numeric @>
        test <@ noInstrCharset r (Special '!') @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Arg alpha-paren`` useWarc =
        let url = Uri $"http://testhost:8000/arg-alpha-paren.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url

        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Special '(', Body) @>
        test <@ hasCharset r (Special ')', Body) @>

        test <@ noCharset r (Upper, Body) @>
        test <@ noCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Arg alpha-paren-br-bt`` useWarc =
        let url = Uri $"http://testhost:8000/arg-alpha-dot-bt.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ execOnly r  @>
        test <@ hasArgExec r  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``No arg static`` useWarc =
        let url = Uri $"http://testhost:8000/static.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ r.Length = 0  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``No arg boring js`` useWarc =
        let url = Uri $"http://testhost:8000/boring-js.html"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ r.Length = 0  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Arg boring js`` useWarc =
        let url = Uri $"http://testhost:8000/boring-js.html?a=123"
        let r = Check.url (ctx.Ctx useWarc) url
        test <@ r.Length = 0  @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Arg cookie refl`` useWarc =
        let url = Uri $"http://testhost:8000/arg-cookie-refl.html?a=123"
        let c = { ctx.Ctx useWarc with InputKinds = Browser.Inputs.Kind.Cookie :: (ctx.Ctx useWarc).InputKinds }
        let r = Check.url c url
        test <@ reflOnly r @>
        test <@ hasCookieCharset r Lower @>

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``Arg cookie exec`` useWarc =
        let url = Uri $"http://testhost:8000/arg-cookie-exec.html?a=123"
        let c = { ctx.Ctx useWarc with InputKinds = Browser.Inputs.Kind.Cookie :: (ctx.Ctx useWarc).InputKinds }
        let r = Check.url c url
        test <@ execOnly r @>


    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``JS Instrumentation basic`` useWarc =
        let c = ctx.Ctx useWarc
        use _ = JsInstrumentation.Sync.withEnabled c.JsInstr true
        let url = Uri $"http://testhost:8000/boring-js.html"
        let navigated = Browser.navigate c.Browser true url 100
        test <@ navigated @>
        Browser.exec c.Browser TextFile<"js/instrumentation_test.js">.Text |> ignore
        let instr = JsInstrumentation.Sync.get c.JsInstr (Browser.exec c.Browser)
        test <@ not instr.IsEmpty @>
        let expectedKeys = [| "Function"; "HTMLElement_addEventListener"; "HTMLElement_innerHTML_set";
                               "HTMLElement_insertAdjacentHTML"; "HTMLElement_outerHTML_set";
                               "HTMLElement_setAttribute"; "HTMLFormElement_action_set";
                               "HTMLInputElement_formAction_set"; "HTMLScriptElement_src_set";
                               "document_write"; "document_writeln"; "eval"; "setInterval"; "setTimeout" |]
        test <@ expectedKeys |> Array.forall (fun i -> Map.containsKey i instr) @>
