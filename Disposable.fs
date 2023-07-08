module Disposable

open System

type Dummy() =
    interface IDisposable with
        member _.Dispose() = ()

type Data<'a>(disp:IDisposable, data:'a) =
    member this.Resource = disp
    member this.Data = data

    interface IDisposable with
        member _.Dispose() =
            disp.Dispose()

type Composite<'a when 'a:> IDisposable>(disps:'a[])=
    member this.Data = disps

    interface IDisposable with
        member _.Dispose() =
            for i in disps do
                i.Dispose()

let dummy() = new Dummy()
let withData disp (dt:'a) = new Data<'a>(disp, dt)
let data dt = withData (dummy()) dt

let composite (disps:'a[]) = new Composite<'a>(disps)
