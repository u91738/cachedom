module Gen

open System
open Config

type Charset = Lower | Upper | Numeric | Special of char

[<DefaultAugmentationAttribute(false)>]
type InputType = Arg of string | Fragment
with
    member this.IsFragment =
        match this with
        | Fragment -> true
        | _ -> false

type InputValue = {
    SearchKey: string
    Url: Uri
}

type Input = {
    Type: InputType
    ExecValues: InputValue[]
    CharsetValues: (Charset*InputValue[])[]
}

let private multiplyS orig arg =
    match orig with
    | "" -> [| arg |]
    | _ -> [| arg; orig + arg; arg + orig |]

let multiplyPayload orig arg =
    multiplyS orig arg.Payload
    |> Array.map (fun i -> {arg with Payload = i})


let private multiply payloads orig =
    Array.collect (multiplyPayload orig) payloads

let private charsets orig =
    [|
        yield Lower, multiplyS orig "abctestcba"
        yield Upper, multiplyS orig "ABCTESTCBA"
        yield Numeric, multiplyS orig "321123"
        for c in "!@#$%^&*()_+='\"\\|/,.<>;:[]{} \x00\x10\x13" do
            yield Special c, multiplyS orig (sprintf "test%ctest" c)
    |]

let private argInputs url payloads =
    [|
        let args = Url.Args.get url
        for i in 0 .. args.Length - 1 do
            let k, v = args[i]
            let exec = multiply payloads v
                       |> Array.map (fun pl -> {SearchKey = pl.Msg; Url = Url.Args.mapNth url i (Url.Args.setVal pl.Payload) })
            yield {
                Type = Arg k
                ExecValues = exec
                CharsetValues = [| for k, pls in charsets v ->
                                    k, pls |> Array.map (fun pl -> { SearchKey = pl; Url = Url.Args.mapNth url i (Url.Args.setVal pl) }) |]
            }
    |]

let private fragInput (url:Uri) payloads =
    let exec = multiply payloads url.Fragment
                |> Array.map (fun pl -> {SearchKey = pl.Msg; Url = Url.Fragment.set url pl.Payload})
    {
        Type = Fragment
        ExecValues = exec
        CharsetValues = [| for k, pls in charsets url.Fragment ->
                            k, pls |> Array.map (fun pl -> { SearchKey = pl; Url = Url.Fragment.set url pl }) |]
    }

let inputs url payloads =
    Array.append (argInputs url payloads) [|fragInput url payloads|]
