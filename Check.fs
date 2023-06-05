module Check

open System
open System.Linq
open System.Text.RegularExpressions
open OpenQA.Selenium
open XssPayload

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

type ResultType = Exec of Uri | Refl of (Uri * Charset * ReflType)[]

type Context = {
    Browser: IWebDriver
    Cache: HttpCache.HttpCache
    Payloads: XssPayload.Group[]
    WaitAfterNavigation: int
}

let private invContains (where:string) (what:string) =
    where.Contains(what, StringComparison.OrdinalIgnoreCase)

let private invEq (a:string) (b:string) =
    a.Equals(b, StringComparison.OrdinalIgnoreCase)

let private hasLog (browser:IWebDriver) value =
    Browser.consoleLog browser |> Seq.exists (invEq value)

let private checkReflected ctx url input mul value =
    use inpCtx = Browser.Inputs.apply ctx.Browser url ctx.WaitAfterNavigation input (Mul.apply mul value)
    [|
        if hasLog ctx.Browser value then
            yield inpCtx.Data, Log
        if invContains ctx.Browser.PageSource value then
            yield inpCtx.Data, Body

        for c in ctx.Browser.Manage().Cookies.AllCookies do
            if invContains c.Name value || invContains c.Value value then
                yield inpCtx.Data, Cookie (c.Name, c.Value)
    |]

let private tryPayload ctx url input mul pl =
    use inpCtx = Browser.Inputs.apply ctx.Browser url ctx.WaitAfterNavigation input (Mul.apply mul pl.Payload)
    if hasLog ctx.Browser pl.Msg then
        Some inpCtx.Data
    else
        None

type CharsetRefl = {
    Sample: string
    Refl: Lazy<(Uri*ReflType)[]>
}

let private getCommonReflections ctx url input =
    [|
        for mul in Mul.all do
            mul, [|
                for cs, sample in Charset.samples do
                    cs, {
                        Sample = sample
                        Refl = lazy checkReflected ctx url input mul sample
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

let private checkInput ctx url input =
    let charsets = getCommonReflections ctx url input
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
        HttpCache.getRecentResponses ctx.Cache
        |> Array.map (fun i -> i.BodyString)
        |> XssPayload.Group.filter ctx.Payloads
        |> Array.allPairs muls
        |> Array.tryPick (fun (mul, pl) -> tryPayload ctx url input mul pl)

    match exec with
    | Some e -> [| input, Exec e |]
    | _ -> [|
        for mul in muls do
            for cs in charsets[mul] do
                if cs.Value.Refl.Value.Any() then
                    input, Refl (Array.map (fun (u, r) -> (u, cs.Key, r)) cs.Value.Refl.Value)
    |]

let url ctx mode url =
    Browser.navigate ctx.Browser false (Uri "about:blank") 0
    HttpCache.clearRecent ctx.Cache
    Browser.navigate ctx.Browser false url ctx.WaitAfterNavigation
    let inputs = Browser.Inputs.get ctx.Browser
    match mode with
    | Brute | Filter when canHaveJs ctx.Cache -> Array.collect (checkInput ctx url) inputs
    | _ -> [| |]
