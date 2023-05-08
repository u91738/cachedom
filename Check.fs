module Check

open System
open System.Text.RegularExpressions
open OpenQA.Selenium
open Gen

type ReflType = Body | Cookie of string*string | Log
type ResultType = Exec of Input * InputValue | Refl of (Charset * ReflType)[]

let private invContains (where:string) (what:string) =
    where.Contains(what, StringComparison.OrdinalIgnoreCase)

let private invEq (a:string) (b:string) =
    a.Equals(b, StringComparison.OrdinalIgnoreCase)

let private consoleMsgEq log msg =
    let lm = Regex.Match(log, "console-api [:0-9]* \"(.*)\"")
    lm.Groups.Count > 1 && invEq lm.Groups[1].Value msg

let private hasLog (browser:IWebDriver) value =
    browser.Manage().Logs.GetLog "browser"
    |> Seq.exists (fun log -> consoleMsgEq log.Message value)

let private getReflected (browser:IWebDriver) value =
    [|
        if hasLog browser value then
            yield Log
        if invContains browser.PageSource value then
            yield Body

        for c in browser.Manage().Cookies.AllCookies do
            if invContains c.Name value || invContains c.Value value then
                yield Cookie (c.Name, c.Value)
    |]

let private checkLog browser refresh waitAfter inp =
    Browser.navigate browser refresh inp.Url waitAfter
    hasLog browser inp.SearchKey

let private checkReflected browser refresh waitAfter inp =
    Browser.navigate browser refresh inp.Url waitAfter
    getReflected browser inp.SearchKey

let url browser payloads url waitAfter =
    [|
        for input in Gen.inputs url payloads do
            match input.ExecValues |> Array.tryFind (checkLog browser input.Type.IsFragment waitAfter) with
            | Some e ->
                yield Exec (input, e)
            | None ->
                let charsets = [|
                        for ch, inputs in input.CharsetValues do
                            for inp in inputs do
                                for rf in checkReflected browser input.Type.IsFragment waitAfter inp do
                                    yield ch, rf
                    |]
                if charsets.Length > 0 then
                    yield charsets |> Array.distinct |> Refl

    |]
