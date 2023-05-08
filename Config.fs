module Config

open System.IO
open Newtonsoft.Json

type XssPayload = {
    [<JsonProperty(Required = Required.Always)>]
    Msg: string
    [<JsonProperty(Required = Required.Always)>]
    Payload: string
}

type Config = {
    [<JsonProperty(Required = Required.Always)>]
    ProxyPort: int
    [<JsonProperty(Required = Required.Always)>]
    ShowBrowser: bool
    [<JsonProperty(Required = Required.Always)>]
    CheckSubUrls: bool
    [<JsonProperty(Required = Required.Always)>]
    UserAgent: string
    [<JsonProperty(Required = Required.Always)>]
    CacheMode: HttpCache.Mode
    [<JsonProperty(Required = Required.Always)>]
    WaitAfterNavigation: int
    [<JsonProperty(Required = Required.Always)>]
    Payloads: XssPayload[]
}

let read filename =
    use jsonReader = new JsonTextReader(new StreamReader(File.OpenRead filename))
    let serializer = JsonSerializer.Create()
    serializer.Deserialize<Config> jsonReader

let Default = read "default.json"
