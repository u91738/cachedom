module Test

open System
open System.Diagnostics
open Xunit
open Swensen.Unquote
open Gen
open Check

type BrowserFixture() =
    let conf = Config.Default
    let cache = HttpCache.empty conf.CacheMode
    let proxy = Proxy.start (ProxyHandler.onRequest false cache) (ProxyHandler.onResponse cache) conf.ProxyPort
    let selproxy = Proxy.selenium "localhost" conf.ProxyPort
    let browser = Browser.get selproxy (not conf.ShowBrowser) conf.UserAgent

    member this.Browser = browser

    with
        interface IDisposable with
            member _.Dispose() =
                browser.Dispose()
                proxy.Dispose()

type ServerFixture() =
    let proc = Process.Start("sh", "../../../test/server.sh")
    with
        interface IDisposable with
            member _.Dispose() =
                proc.Dispose()

let isExec = function | Exec _ -> true | _ -> false

let execOnly r = Array.length r > 0 && Array.forall isExec r

let reflOnly r = Array.length r > 0 && Array.forall (isExec >> not) r

let hasFragExec =
    Array.exists (function
                  | Exec (i, _) when i.Type.IsFragment -> true
                  | _ -> false)

let hasArgExec =
    Array.exists (function
                  | Exec (i, _) when not i.Type.IsFragment -> true
                  | _ -> false)

let hasCharset r cs =
    Array.exists (function
                  | Refl refl -> Array.contains cs refl
                  | _ -> false) r

let noCharset r cs = not (hasCharset r cs)

type Tests(ctx:BrowserFixture, srv:ServerFixture) =
    interface IClassFixture<BrowserFixture>
    interface IClassFixture<ServerFixture>

    [<Fact>]
    member _.``Param write``() =
        let url = Uri "http://localhost:8000/arg-write.html?a=123"
        let r = Check.url ctx.Browser Config.Default.Payloads url 0
        test <@ execOnly r  @>
        test <@ hasArgExec r  @>

    [<Fact>]
    member _.``Fragment eval``() =
        let url = Uri "http://localhost:8000/frag-eval.html?a=123"
        let r = Check.url ctx.Browser Config.Default.Payloads url 0
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Fragment setTimeout``() =
        let url = Uri "http://localhost:8000/frag-timeout.html?a=123"
        let r = Check.url ctx.Browser Config.Default.Payloads url Config.Default.WaitAfterNavigation
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Param write alnum``() =
        let url = Uri "http://localhost:8000/arg-write-alnum.html?a=123"
        let r = Check.url ctx.Browser Config.Default.Payloads url 0
        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Upper, Body) @>
        test <@ hasCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>

    [<Fact>]
    member _.``Fragment alpha-paren``() =
        let url = Uri "http://localhost:8000/frag-alpha-paren.html?a=123"
        let r = Check.url ctx.Browser Config.Default.Payloads url 0

        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Special '(', Body) @>
        test <@ hasCharset r (Special ')', Body) @>

        test <@ noCharset r (Upper, Body) @>
        test <@ noCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>
