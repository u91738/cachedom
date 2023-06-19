module JsInstrumentation

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open System.Threading
open FSharp.Data.LiteralProviders
open Newtonsoft.Json

let private instrScript = TextFile<"js/instrumentation.js">.Text
                          |> sprintf "<head><script>%s</script>"

let private jsBodyRegex = Regex("javascript|<script|on[a-z0-9_\\-]*=|<svg", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)
let canHaveJs (body:string) = jsBodyRegex.IsMatch body

let add (contentType: string) (body: string) =
    if (contentType |> String.IsNullOrEmpty |> not) &&
       (body |> String.IsNullOrEmpty |> not) &&
       contentType.Contains("text/html", StringComparison.InvariantCultureIgnoreCase) &&
       canHaveJs body then
        Some (body.Replace("<head>", instrScript, StringComparison.InvariantCultureIgnoreCase))
    else
        None

type InstrumentationRecord = {
    [<JsonProperty(Required = Required.Always)>]
    Stack: string
    [<JsonProperty(Required = Required.Always)>]
    Args: string[]
}

let private parseResult json =
    if String.IsNullOrEmpty json || json = "null" then
        Dictionary()
    else
        JsonConvert.DeserializeObject<Dictionary<string, InstrumentationRecord[]>>(json)

let private isInstrCall (call:InstrumentationRecord) =
    call.Args |> Array.forall (fun i -> i.Contains "script_instrumentation_result")

let private cleanResult (res: Dictionary<string, InstrumentationRecord[]>) =
    // remove instrumentation records caused by instrumentation code itself
    res
    |> Seq.map (fun i -> i.Key, i.Value |> Array.filter (isInstrCall >> not))
    |> Seq.filter (snd >> Array.isEmpty >> not)
    |> Map.ofSeq

let get (execJs: string->string) =
    execJs TextFile<"js/instrumentation_result.js">.Text
    |> parseResult
    |> cleanResult

module Sync =
    type Instrumentation = private {
        mutable Enabled: bool
        Lock: obj
    }

    let create enabled = { Enabled = enabled; Lock = obj() }

    let setEnabled i v = Volatile.Write(&i.Enabled, v)

    let isEnabled i = Volatile.Read &i.Enabled

    let add i contentType body =
        lock i.Lock (fun () ->
            if i.Enabled then
                add contentType body
            else None )

    let get i f =
        lock i.Lock (fun () ->
            if i.Enabled then
                get f
            else
                Map.empty )

    type InstrumentationContext(i:Instrumentation, initial: bool, enabled:bool) =
        new (i:Instrumentation, enabled:bool) =
            let ini = isEnabled i
            setEnabled i enabled
            new InstrumentationContext(i, ini, enabled)

        interface IDisposable with
            member _.Dispose() =
                setEnabled i initial

    let withEnabled i v = new InstrumentationContext(i, v)
