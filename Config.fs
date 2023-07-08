module Config

open System.IO
open Newtonsoft.Json



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
    CacheFailMode: ProxyHandler.NoCache
    [<JsonProperty(Required = Required.Always)>]
    WaitAfterNavigation: int
    [<JsonProperty(Required = Required.Always)>]
    JsBodyFilter: bool
    [<JsonProperty(Required = Required.Always)>]
    IgnoreCookies:bool
    [<JsonProperty(Required = Required.Always)>]
    Payloads: XssPayload.Group[]
}

let read filename =
    use jsonReader = new JsonTextReader(new StreamReader(File.OpenRead filename))
    let serializer = JsonSerializer.Create()
    serializer.Deserialize<Config> jsonReader

let Default = read "default.json"
