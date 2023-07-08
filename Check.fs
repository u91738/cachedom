module Check

open System
open System.Linq
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

type ReflType = Body
              | Cookie of string * string
              | Log
              | Instr of string * JsInstrumentation.InstrumentationRecord

type ResultType = Exec of Uri | Refl of (Uri * Charset * ReflType)[]

type PayloadFilter = Filter | Brute

type Context = {
    Browser: IWebDriver
    Cache: HttpCache.Recent
    JsInstr: JsInstrumentation.Sync.Instrumentation
    Payloads: XssPayload.Group[]
    WaitAfterNavigation: int
    FilterMode: PayloadFilter
    InputKinds: list<Browser.Inputs.Kind>
}

let private invContains (where:string) (what:string) =
    where.Contains(what, StringComparison.OrdinalIgnoreCase)

let private invEq (a:string) (b:string) =
    a.Equals(b, StringComparison.OrdinalIgnoreCase)

let private hasLog (browser:IWebDriver) value =
    Browser.consoleLog browser |> Seq.exists (invEq value)

let private checkReflected ctx url input mul value =
    use _ = JsInstrumentation.Sync.withEnabled ctx.JsInstr true
    use inpCtx = Browser.Inputs.apply ctx.Browser url ctx.WaitAfterNavigation input (Mul.apply mul value)
    match inpCtx.Data with
    | Some url ->
        [|
            if hasLog ctx.Browser value then
                yield url, Log
            if invContains ctx.Browser.PageSource value then
                yield url, Body

            for c in ctx.Browser.Manage().Cookies.AllCookies do
                if invContains c.Name value || invContains c.Value value then
                    yield url, Cookie (c.Name, c.Value)

            for call in JsInstrumentation.Sync.get ctx.JsInstr (Browser.execDefault ctx.Browser) do
                for case in call.Value do
                    if case.Args |> Array.exists (fun i -> invContains i value) then
                        yield url, Instr (call.Key, case)
        |]
    | None -> [| |]

let private tryPayload ctx url input mul pl =
    use _ = JsInstrumentation.Sync.withEnabled ctx.JsInstr false
    use inpCtx = Browser.Inputs.apply ctx.Browser url ctx.WaitAfterNavigation input (Mul.apply mul pl.Payload)
    inpCtx.Data |> Option.bind (fun url -> if hasLog ctx.Browser pl.Msg then Some url else None)

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

let private canHaveJs (cache:HttpCache.Recent) =
    cache.Recent.Keys()
    |> Array.filter (fun (method, _) -> "get".Equals(method, StringComparison.InvariantCultureIgnoreCase))
    |> Array.choose (fun (m, u) -> cache.Recent.Get m u)
    |> Array.exists (fun i -> JsInstrumentation.canHaveJs i.BodyString)

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
        ctx.Cache.Recent.Keys()
        |> Array.choose (fun (m, u) -> ctx.Cache.Recent.Get m u)
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

let url ctx url =
    Browser.navigate ctx.Browser false (Uri "about:blank") 0 |> ignore
    if Browser.navigate ctx.Browser false url ctx.WaitAfterNavigation then
        let inputs = Browser.Inputs.get ctx.Browser ctx.InputKinds
        match ctx.FilterMode with
        | Brute | Filter when canHaveJs ctx.Cache -> Array.collect (checkInput ctx url) inputs
        | _ -> [| |]
    else
        failwithf "Failed to navigate to %A" url
