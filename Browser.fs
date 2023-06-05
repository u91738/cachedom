module Browser

open System
open System.IO
open System.Reflection
open System.Threading
open OpenQA.Selenium
open OpenQA.Selenium.Chrome

let get proxy isHeadless userAgent : IWebDriver =
    let opts = ChromeOptions()
    opts.UnhandledPromptBehavior <- UnhandledPromptBehavior.Accept
    opts.SetLoggingPreference("browser", LogLevel.All)

    opts.AddArguments [|
         "--disable-dev-shm-usage"
         "--ignore-certificate-errors"
         |]
    if isHeadless then
        opts.AddArgument "--headless"

    opts.AddArgument ("user-agent=" + userAgent)
    opts.Proxy <- proxy
    let localDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    let r = new ChromeDriver(localDir, opts)
    r.Manage().Timeouts().PageLoad <- TimeSpan.FromSeconds 10.0
    r

let private waitUrlChange (browser:IWebDriver) prevUrl =
    if browser.Url <> prevUrl then
        while browser.Url = prevUrl do
            Thread.Sleep 50

let navigate (browser:IWebDriver) refresh (url:Uri) (waitAfter:int) =
    let u = browser.Url
    let nav = browser.Navigate()
    nav.GoToUrl url
    if refresh then
        nav.Refresh() // fragment change will have no effect without refresh
    waitUrlChange browser u
    Thread.Sleep waitAfter

module Cookie =

    let all (browser:IWebDriver) =
        browser.Manage().Cookies.AllCookies |> Seq.toArray

    let copy (c:Cookie) =
        Cookie(c.Name, c.Value, c.Domain, c.Path, c.Expiry, c.Secure, c.IsHttpOnly, c.SameSite)

    type Context(browser:IWebDriver, cookie:Cookie) =
        let oldCookie =
            let cookies = browser.Manage().Cookies
            let c = cookies.GetCookieNamed cookie.Name
            cookies.DeleteCookieNamed cookie.Name
            cookies.AddCookie cookie
            c

        interface IDisposable with
            member _.Dispose() =
                let cookies = browser.Manage().Cookies
                cookies.DeleteCookieNamed cookie.Name
                if not(isNull oldCookie) then
                    cookies.AddCookie oldCookie

module Inputs =
    [<DefaultAugmentationAttribute(false)>]
    type Input = Arg of int | Fragment | Cookie of Cookie
    with
        member this.IsFragment =
            match this with
            | Fragment -> true
            | _ -> false

        member this.IsArg =
            match this with
            | Arg _ -> true
            | _ -> false

        member this.IsCookie =
            match this with
            | Cookie _ -> true
            | _ -> false

    let get (browser:IWebDriver) =
        [|
            for i in 0 .. Url.Args.count (Uri browser.Url) - 1 do
                Arg i
            for c in Cookie.all browser do
                Cookie c
            Fragment
        |]

    let apply browser uri waitAfter input transform =
        let navUri = match input with
                     | Arg i -> Url.Args.mapNth uri i (fun k v -> k, transform v)
                     | Fragment -> Url.Fragment.map uri transform
                     | Cookie _ -> uri
        let ctx = match input with
                  | Arg _  | Fragment -> Disposable.data navUri
                  | Cookie c -> Disposable.withData (new Cookie.Context(browser, c)) navUri
        navigate browser (not input.IsArg) navUri waitAfter
        ctx
