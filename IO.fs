module IO

open System.IO
open System.Text

let readLines (f:StreamReader) = seq {
    let res = StringBuilder()
    let mutable pos = 0
    let mutable chi = f.Read() // ReadLine treats \r\n and \n the same, breaks offsets
    while chi <> -1 do
        let ch = char chi
        if ch = '\n' then
            //printfn "%d, %s" pos (res.ToString())
            yield pos, res.ToString().TrimEnd('\r')
            pos <- pos + res.Length + 1
            res.Clear() |> ignore
        else
            res.Append ch |> ignore

        chi <- f.Read()
}

let readChunk (f:Stream) offset len =
    f.Seek(offset, SeekOrigin.Begin) |> ignore
    let buf = Array.zeroCreate (int len)
    f.Read(buf, 0, buf.Length) |> ignore
    Encoding.UTF8.GetString buf
