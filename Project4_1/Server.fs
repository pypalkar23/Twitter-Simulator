open System
open Akka
open Akka.Actor
open Akka.Remote
open Akka.FSharp
open Akka.Configuration


let configuration =
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote.helios.tcp {
                transport-protocol = tcp
                port = 5000
                hostname = 0.0.0.0
            }
        }"
    )

let twitterServerSystem = ActorSystem.Create("TwitterServerSystem", configuration)
let mutable serverMainActorRef = null

type MainCommands =
    | Initialize

type ServerUserCommands =
    | Register of (int)
    | Login of (int)
    | Logout of (int)
    | Follow of (int*int)


let ServerUserActor (mailbox:Actor<_>) =    
    let mutable actorsDone = 0


    let rec loop () = 
        actor {
            let! (message) = mailbox.Receive()

            match message with 
            | Register(userId) ->
                printfn "Registering new user %d" userId
            | Login(userId) ->
                printfn "Logout %d" userId
            | Logout(userId) ->
                printfn "Logout %d" userId
            | Follow(myId, toFollowId) ->
                 printfn "Follow Request from %d to follow %d" myId toFollowId 
            | _ -> ()

            return! loop()
        }
    loop()


let ServerMainActor (mailbox:Actor<_>) =    
    let mutable actorsDone = 0

    let rec loop () = 
        actor {
            let! (message) = mailbox.Receive()

            match message with 
            | Initialize(_) ->
                printfn "Start"
                
            | _ -> ()

            return! loop()
        }
    loop()

let main argv =
    
    serverMainActorRef <- spawn twitterServerSystem "ServerMainActor" ServerMainActor
    serverMainActorRef <! Initialize

    0