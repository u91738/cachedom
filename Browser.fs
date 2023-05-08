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
