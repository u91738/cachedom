module Disposable

open System

type Dummy() =
    interface IDisposable with
        member _.Dispose() = ()

type Data<'a>(disp:IDisposable, data:'a) =
    member this.Data = data

    interface IDisposable with
        member _.Dispose() =
            disp.Dispose()

let dummy() = new Dummy()
let withData disp (dt:'a) = new Data<'a>(disp, dt)
let data dt = withData (dummy()) dt