module Proxy

open System
open System.Net
open System.Threading.Tasks

open Titanium.Web.Proxy
open Titanium.Web.Proxy.Models
open Titanium.Web.Proxy.EventArguments

let private onSessionEvent (f:'a->Task<unit>) =
    let impl _ (e: 'a): Task =
        f e
    AsyncEventHandler<'a> impl

let selenium host port =
    let addr = sprintf "%s:%d" host port
    let p = OpenQA.Selenium.Proxy()
    p.Kind <- OpenQA.Selenium.ProxyKind.Manual
    p.IsAutoDetect <- false
    p.HttpProxy <- addr
    p.SslProxy <- addr
    p

let start beforeRequest beforeResponse port =
    let server = new ProxyServer()
    server.CertificateManager.CreateRootCertificate false |> ignore
    server.CertificateManager.TrustRootCertificate true

    server.ConnectionTimeOutSeconds <- 10
    server.ConnectTimeOutSeconds <- 10

    server.AddEndPoint(ExplicitProxyEndPoint(IPAddress.Loopback, port, true))

    server.add_BeforeRequest (onSessionEvent beforeRequest)
    server.add_BeforeResponse (onSessionEvent beforeResponse)
    server.Start()
    server :> IDisposable
