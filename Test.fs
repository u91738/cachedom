module Test

open System
open System.Diagnostics
open System.Net.Sockets
open System.Net.NetworkInformation
open Xunit
open Swensen.Unquote

open Check


let localhost = // chromium is too smart about local addresses and proxies, use external address
    let iface = NetworkInterface.GetAllNetworkInterfaces()
                |> Array.find (fun i ->
                    i.NetworkInterfaceType <> NetworkInterfaceType.Loopback &&
                    i.OperationalStatus = OperationalStatus.Up
                )
    let ip = iface.GetIPProperties().UnicastAddresses
             |> Seq.find (fun ip -> ip.Address.AddressFamily = AddressFamily.InterNetwork)
    string ip.Address



type BrowserFixture() =
    let conf = Config.Default
    let cache = HttpCache.empty conf.CacheMode
    let proxy = Proxy.start (ProxyHandler.onRequest false cache) (ProxyHandler.onResponse cache) conf.ProxyPort
    let selproxy = Proxy.selenium "localhost" conf.ProxyPort
    let browser = Browser.get selproxy (not conf.ShowBrowser) conf.UserAgent


    member this.Ctx = {
        Browser = browser
        Cache = cache
        Payloads = conf.Payloads
        WaitAfterNavigation = 110
    }

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

let isExec = function | _, Exec _ -> true | _, _ -> false

let execOnly r = Array.length r > 0 && Array.forall isExec r

let reflOnly r = Array.length r > 0 && Array.forall (isExec >> not) r

let hasFragExec (r: (Url.Inputs.Input*_)[]) =
    r |> Array.exists (function
                       | i, Exec (_) when i.IsFragment -> true
                       | _ -> false)

let hasArgExec (r: (Url.Inputs.Input*_)[]) =
    r |> Array.exists (function
                     | i, Exec (_) when not i.IsFragment -> true
                     | _ -> false)

let hasCharset r cs =
    Array.exists (function
                  | _, Refl refl -> Array.contains cs refl
                  | _ -> false) r

let noCharset r cs = not (hasCharset r cs)

let filter = Filter // Brute //Filter

type Tests(ctx:BrowserFixture, srv:ServerFixture) =
    interface IClassFixture<BrowserFixture>
    interface IClassFixture<ServerFixture>

    [<Fact>]
    member _.``Param write``() =
        let url = Uri $"http://{localhost}:8000/arg-write.html?a=123"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasArgExec r  @>

    [<Fact>]
    member _.``Angular frag write``() =
        let url = Uri $"http://{localhost}:8000/angular.html"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Vue2 frag``() =
        let url = Uri $"http://{localhost}:8000/vue2.html"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Vue3 components``() =
        let url = Uri $"http://{localhost}:8000/vue3-comp.html"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Vue2 components``() =
        let url = Uri $"http://{localhost}:8000/vue2-comp.html"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Vue3 frag``() =
        let url = Uri $"http://{localhost}:8000/vue3.html"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Fragment eval``() =
        let url = Uri $"http://{localhost}:8000/frag-eval.html?a=123"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Fragment setTimeout``() =
        let url = Uri $"http://{localhost}:8000/frag-timeout.html?a=123"
        let c = { ctx.Ctx with WaitAfterNavigation = Config.Default.WaitAfterNavigation }
        let r = Check.url c filter url
        test <@ execOnly r  @>
        test <@ hasFragExec r  @>

    [<Fact>]
    member _.``Param write alnum``() =
        let url = Uri $"http://{localhost}:8000/arg-write-alnum.html?a=123"
        let r = Check.url ctx.Ctx filter url
        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Upper, Body) @>
        test <@ hasCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>

    [<Fact>]
    member _.``Arg alpha-paren``() =
        let url = Uri $"http://{localhost}:8000/arg-alpha-paren.html?a=123"
        let r = Check.url ctx.Ctx filter url

        test <@ reflOnly r @>
        test <@ hasCharset r (Lower, Body) @>
        test <@ hasCharset r (Special '(', Body) @>
        test <@ hasCharset r (Special ')', Body) @>

        test <@ noCharset r (Upper, Body) @>
        test <@ noCharset r (Numeric, Body) @>
        test <@ noCharset r (Special '!', Body) @>
        test <@ noCharset r (Special '!', Log) @>

    [<Fact>]
    member _.``Arg alpha-paren-br-bt``() =
        let url = Uri $"http://{localhost}:8000/arg-alpha-dot-bt.html?a=123"
        let r = Check.url ctx.Ctx filter url
        test <@ execOnly r  @>
        test <@ hasArgExec r  @>
