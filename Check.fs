module Check

open System
open System.Linq
open System.Collections.Generic
open System.Text.RegularExpressions
open OpenQA.Selenium

type private Mul = Id | Prefix | Postfix
module private Mul =
    let all = [|Id; Prefix; Postfix|]

    let apply mt arg (orig:string) =
        match mt with
        | Id -> arg
        | Prefix -> arg + orig
        | Postfix -> orig + arg

type Charset = Lower | Upper | Numeric | Special of Char
module Charset =
    let common = Set.ofArray [| Numeric; Lower; Upper|]

    let samples = [|
            Numeric, "321123"
            Lower, "abctestcba"
            Upper, "ABCTESTCBA"
            for i in "!@#$%^&*()_+='\"\\|/,.<>;:[]{} \x00\x10\x13".ToCharArray() do
                Special i, sprintf "test%ctest" i
        |]

type ReflType = Body | Cookie of string*string | Log

type ResultType = Exec of Uri | Refl of (Charset * ReflType)[]

type Context = {
    Browser: IWebDriver
    Cache: HttpCache.HttpCache
    Payloads: Config.XssPayloadGroup[]
    WaitAfterNavigation: int
}

let private invContains (where:string) (what:string) =
    where.Contains(what, StringComparison.OrdinalIgnoreCase)

let private invEq (a:string) (b:string) =
    a.Equals(b, StringComparison.OrdinalIgnoreCase)

let private consoleMsgEq log msg =
    [| "console-api [:0-9]* \"(.*)\""; "console-api [:0-9]* (.*)" |]
    |> Array.exists (fun rex ->
        let lm = Regex.Match(log, rex)
        lm.Groups.Count > 1 && invEq lm.Groups[1].Value msg
    )

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

let private checkReflected ctx refresh url searchKey =
    Browser.navigate ctx.Browser refresh url ctx.WaitAfterNavigation
    getReflected ctx.Browser searchKey

let private matchExists regexPattern (strings: string[]) =
    let rex = Regex(regexPattern)
    strings |> Array.exists rex.IsMatch

let private filterPayloads cache (payloads:Config.XssPayloadGroup[]) =
    let recentResps = HttpCache.getRecentResponses cache |> Array.map (fun i -> i.BodyString)
    [|
        for grp in payloads do
            if String.IsNullOrEmpty grp.Regex || matchExists grp.Regex recentResps then
                yield! grp.Payloads
    |]

let private tryPayload ctx url input mul (pl:Config.XssPayload) =
    let plUrl = Url.Inputs.map url (Mul.apply mul pl.Payload) input
    Browser.navigate ctx.Browser input.IsFragment plUrl ctx.WaitAfterNavigation
    if hasLog ctx.Browser pl.Msg then
        Some plUrl
    else
        None

type CharsetRefl = {
    Sample: string
    Url: Uri
    Refl: Lazy<ReflType[]>
}

let private getCommonReflections ctx url input =
    [|
        for mul in Mul.all do
            mul, [|
                for cs, sample in Charset.samples do
                    let csUrl = Url.Inputs.map url (Mul.apply mul sample) input
                    cs, {
                        Sample = sample
                        Url = csUrl
                        Refl = lazy checkReflected ctx input.IsFragment csUrl sample
                    }
            |] |> Map.ofArray
    |] |> Map.ofArray


let private jsBodyRegex = Regex("javascript|<script|on[a-z0-9_\\-]*=|<svg", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

let private canHaveJs cache =
    HttpCache.getAll cache
    |> Array.exists (fun i ->
        (fst i.Key).Equals("get", StringComparison.InvariantCultureIgnoreCase) &&
        jsBodyRegex.IsMatch i.Value.BodyString
    )

type PayloadFilter = Filter | Brute

let private checkJsInput ctx url input (charsets:Map<Mul, Map<Charset, CharsetRefl>>) =
    let idCs = Set.ofArray [|
        for i in charsets[Id] do
            if Charset.common.Contains i.Key && i.Value.Refl.Value.Any() then
                i.Key
    |]
    let muls = if idCs = Charset.common then
                    [| Id |]
                else
                    Mul.all

    let exec =
        filterPayloads ctx.Cache ctx.Payloads
        |> Array.allPairs muls
        |> Array.tryPick (fun (mul, pl) -> tryPayload ctx url input mul pl)

    match exec with
    | Some e -> [| input, Exec e |]
    | _ -> [|
        for mul in muls do
            for cs in charsets[mul] do
                if cs.Value.Refl.Value.Any() then
                    input, Refl (Array.map (fun r -> (cs.Key, r)) cs.Value.Refl.Value)
    |]

let private checkInput ctx mode url input =
    let charsets = getCommonReflections ctx url input
    match mode with
    | Brute -> checkJsInput ctx url input charsets
    | Filter ->
        charsets[Id].[Numeric].Refl.Force() |> ignore
        if canHaveJs ctx.Cache then
            checkJsInput ctx url input charsets
        else
            [| |]

let url ctx mode url =
    HttpCache.clearRecent ctx.Cache
    Url.Inputs.get url |> Array.collect (checkInput ctx mode url)
