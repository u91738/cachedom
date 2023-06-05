module XssPayload

open System
open System.Text.RegularExpressions
open Newtonsoft.Json

type XssPayload = {
    [<JsonProperty(Required = Required.Always)>]
    Msg: string
    [<JsonProperty(Required = Required.Always)>]
    Payload: string
}

type Group = {
    [<JsonProperty(Required = Required.Default)>]
    Regex: string
    [<JsonProperty(Required = Required.Always)>]
    Payloads: XssPayload[]
}
module Group =
    let private matchExists regexPattern (strings: string[]) =
        let rex = Regex(regexPattern)
        strings |> Array.exists rex.IsMatch

    let filter (payloads:Group[]) bodies =
        [|
            for grp in payloads do
                if String.IsNullOrEmpty grp.Regex || matchExists grp.Regex bodies then
                    yield! grp.Payloads
        |]
