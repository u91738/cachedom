module Enumerator

open System.Collections.Generic

let split f (e: IEnumerator<'a>) = seq {
    let r = ResizeArray()
    while e.MoveNext() do
        if f e.Current then
            yield r.ToArray()
            r.Clear()
        r.Add e.Current
    if r.Count > 0 then
        yield r.ToArray()
}

let skipWhile f (e: IEnumerator<'a>) =
    if f e.Current then
        while e.MoveNext() && f e.Current do
            ()

let toSeq (e: IEnumerator<'a>) = seq {
    yield e.Current
    while e.MoveNext() do
        yield e.Current

}
