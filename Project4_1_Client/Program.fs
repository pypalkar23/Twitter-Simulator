
open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open FSharp.Data
open Akka.Serialization

(*
{
    "local_ip":"localhost",
    "port":7500,
    "id": "mandar",
    "userCount":1000,
    "clientCount":1000,
    "server_ip":"0.0.0.0",
    "server_port":"6500",
}
*)

[<Literal>]
let resolutionFolder = __SOURCE_DIRECTORY__
type configJson = JsonProvider<"config.json", ResolutionFolder = resolutionFolder>
let values = configJson.GetSample()

//config read from the file
let local_ip = values.LocalIp
let port = values.Port.ToString()
let id = values.Id
let usersPerClient = values.UserCount
let clientCount = values.ClientCount
let serverip = values.ServerIp

// Configuration
let config = 
    ConfigurationFactory.ParseString(
        sprintf @"akka {            
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
             serializers {
                    hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
            }
            serialization-bindings {
                    ""System.Object"" = hyperion
            }             
            remote.helios.tcp {
                transport-protocol = tcp
                port = %s
                hostname = %s
            }
    }" port local_ip)

let system = ActorSystem.Create("TwitterClient", config)


type BossMessage = 
    | Start of (string*int*int*string)
    | RegisterUser of (int)
    | Offline
    | AckClientReg
    | AckOnline of (string)
    | AckUserReg of (string*string)

type FollowMessages = 
    | Init of (list<string>*int)

type UserMessages = 
    | Ready of (string*list<string>*ActorSelection*int*string*List<string>*int)
    | GoOnline
    | GoOffline
    | Action
    | ActionTweet

// Printer Actor - To print the output

let printerActor (mailbox:Actor<_>) = 
    let rec loop () = actor {
        let! (message:obj) = mailbox.Receive()
        printfn "%A" message
        return! loop()
    }
    loop()

let printerRef = spawn system "Printer" printerActor
//1 - Tweet, 2 - Retweet, 3 - Follow, 4 - Tweet with only hashtags, 5 - Tweet with mentions and hashtags, 6 - QueryHashtags, 7 - QueryMentions
let UserActor (mailbox:Actor<_>) = 
    let mutable myId = ""
    let mutable isOnline = false
    let mutable clientList = []
    let mutable server = ActorSelection()
    let mutable usersCount = 0
    let followRand = Random()
    let htagRand = Random()
    let mentionsRand = Random()
    let htagRandReq = Random()
    let mentionsRandReq = Random()
    let clientRand = Random()
    let mutable cliId = ""
    let mutable topHashTags = []
    let mutable tweetCount = 0
    let mutable interval = 0.0
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        match message with
        | Ready(mid,clist,ser,nusers,cid,htList,time) ->
            myId <- mid
            clientList <- clist
            isOnline <- true
            server <- ser
            usersCount <- nusers
            cliId <- cid
            topHashTags <- htList
            interval <- time |> float
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(50.0), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(49.0), mailbox.Self, ActionTweet)
        | Action ->
            if isOnline then
                //1 - Tweet, 2 - Retweet, 3 - Follow, 4 - Tweet with only hashtags, 5 - Tweet with mentions and hashtags, 6 - QueryHashtags, 7 - QueryMentions
                let actions = ["Follow"; "QueryHashtags"; "QueryMentions"]
                let actionsrand = Random()
                let act = actions.[actionsrand.Next(actions.Length)]
                match act with
                | "Follow" ->
                    let mutable fUser = [1 .. usersCount].[followRand.Next(usersCount)] |> string
                    let mutable randclid = clientList.[clientRand.Next(clientList.Length)]
                    let mutable followUser = sprintf "%s_%s" randclid fUser
                    while followUser = myId do 
                        fUser <- [1 .. usersCount].[followRand.Next(usersCount)] |> string
                        followUser <- sprintf "%s_%s" randclid fUser 
                    server <! ("Follow",cliId,myId,followUser,DateTime.Now)
                | "QueryHashtags" ->
                    let hashTag = topHashTags.[htagRandReq.Next(topHashTags.Length)]
                    server <! ("QueryHashtags",cliId,myId,hashTag,DateTime.Now)
                | "QueryMentions" ->
                    let mutable mUser = [1 .. usersCount].[mentionsRandReq.Next(usersCount)] |> string
                    let mutable randclid = clientList.[clientRand.Next(clientList.Length)]
                    let mutable mentionsUser = sprintf "%s_%s" randclid mUser
                    server <! ("QueryMentions",cliId,myId,mentionsUser,DateTime.Now)
                | _ ->
                    ignore()
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0), mailbox.Self, Action)
        | ActionTweet ->
            if isOnline then
                let actions = ["Tweet with only hashtags"; "Tweet"; "Retweet"; "Tweet with mentions and hashtags"]
                let actionsrand = Random()
                let act = actions.[actionsrand.Next(actions.Length)]
                match act with
                | "Tweet with only hashtags" ->
                    let timestamp = DateTime.Now
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted -> tweet_%d" myId tweetCount
                    server <! ("Tweet",cliId,myId,tweetMsg,timestamp)
                | "Tweet" ->
                    let timestamp = DateTime.Now
                    server <! ("ReTweet",cliId,myId,sprintf "user %s doing re-tweet" myId,timestamp)  
                | "Retweet" ->
                    let timestamp = DateTime.Now
                    let hashTag = topHashTags.[htagRand.Next(topHashTags.Length)]
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted -> tweet_%d with hashtag #%s" myId tweetCount hashTag
                    server <! ("Tweet",cliId,myId,tweetMsg,timestamp)
                | "Tweet with mentions and hashtags" ->
                    let timestamp = DateTime.Now
                    let mutable mUser = [1 .. usersCount].[mentionsRand.Next(usersCount)] |> string
                    let mutable randclid = clientList.[clientRand.Next(clientList.Length)]
                    let mutable mentionsUser = sprintf "%s_%s" randclid mUser
                    while mentionsUser = myId do 
                        mUser <- [1 .. usersCount].[mentionsRand.Next(usersCount)] |> string
                        mentionsUser <- sprintf "%s_%s" randclid mUser 
                    let hashTag = topHashTags.[htagRand.Next(topHashTags.Length)]
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted tweet_%d with hashtag #%s and mentioned @%s" myId tweetCount hashTag mentionsUser
                    server <! ("Tweet",cliId,myId,tweetMsg,timestamp) 
                | _ ->
                    ignore()   
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(interval), mailbox.Self, ActionTweet)                                                           
        | GoOffline ->
            isOnline <- false
        | GoOnline ->
            isOnline <- true
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(101.0), mailbox.Self, ActionTweet)
        return! loop()
    }
    loop()

let ClientAdminActor (mailbox:Actor<_>) = 
    let mutable id = ""
    let mutable nusers = 0
    let mutable nclients = 0
    let mutable port = "6500"
    let mutable clientslist = []
    let mutable cur_offline = Set.empty
    let mutable registered_list = []
    let mutable useraddress = Map.empty
    let mutable intervalmap = Map.empty
    let mutable usersList = []
    let mutable subsrank = Map.empty
    let server = system.ActorSelection(
                   sprintf "akka.tcp://TwitterServer@%s:%s/user/ServerRequestsHandler" serverip port)
    
    let hashTagsList = ["lockdown";"metoo";"covid19";"blacklivesmatter";"crypto";"crowdfunding";"giveaway";"contest";
                        "blackhistorymonth";"womenshistorymonth";"cryptocurrency";"womensday";"happybirthday";
                        "authentication";"USelections";"bidenharris";"internationalwomensday";"influencermarketing";
                        "distributedsystems";"gogators";"blackfriday";"funny";"womeninstem";"iwon";"photography";
                        "mondaymotivation";"ootd";"vegan";"traveltuesday";"tbt"]

    let rec loop () = actor {
        let! (msg:obj) = mailbox.Receive() 
        let sender = mailbox.Sender()
        match msg with
        | :? BossMessage as bm ->
            match bm with 
            | Start(idParam, numUsers, numClients, portParam) ->
                let timestamp = DateTime.Now
                //let (_,i, u, n, p) : Tuple<string,string,string,string,string> = downcast message 
                id <- idParam
                nusers <- numUsers
                nclients <- numClients
                port <- portParam
                printerRef <! sprintf "Starting twitter client with Id: %s " id
                let mutable usersarr = [| 1 .. nusers |]
                // printfn "usersarr=%A" usersarr
                let rand = Random()
                
                let swap (a: _[]) x y =
                    let tmp = a.[x]
                    a.[x] <- a.[y]
                    a.[y] <- tmp
                
                let shuffle a =
                    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a

                shuffle usersarr
                usersList <- usersarr |> Array.toList
                // printfn "second userarr=%A" usersarr
                for i in [1 .. nusers] do
                    let userkey = usersarr.[i-1] |> string
                    subsrank <- Map.add (sprintf "%s_%s" id userkey) ((nusers-1)/i) subsrank
                    intervalmap <- Map.add (sprintf "%s_%s" id userkey) i intervalmap

                server <! ("ClientRegister",id,local_ip,port,timestamp)
                for i in [1 .. nclients] do
                    let istr = i |> string
                    clientslist <- istr :: clientslist
            | AckClientReg ->
                mailbox.Self <! RegisterUser(1)
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, Offline)
            | RegisterUser(nextId) ->
                let timestamp = DateTime.Now
                //let (_,nextid,_,_,_) : Tuple<string,string,string,string,string> = downcast message 
                let mutable numcurid = nextId |> int32
                let mutable curid = sprintf "%s_%s" id (usersList.[numcurid-1] |> string) 
                let ref = spawn system (sprintf "User_%s" curid) UserActor
                useraddress <- Map.add curid ref useraddress
                let subsstr = subsrank.[curid] |> string
                server <! ("UserRegister", id, curid, subsstr,timestamp)
                registered_list <- curid :: registered_list

                if numcurid < nusers then
                    numcurid <- numcurid+1
                    let stnumcurid = numcurid
                    system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(50.0), mailbox.Self, RegisterUser(numcurid))
            | AckUserReg(uid,msg) ->
                printerRef <! msg
                let mutable baseInterval = nusers/100
                if baseInterval < 5 then
                    baseInterval <- 5            
                useraddress.[uid] <! Ready(uid,clientslist,server,nusers,id,hashTagsList,(baseInterval*intervalmap.[uid]))
            | Offline ->
                let timestamp = DateTime.Now
                // printerRef <! sprintf "Users going offline & online: %A" cur_offline
                let offlinerand = Random()
                let mutable total = registered_list.Length
                total <- (30*total)/100
                let mutable newset = Set.empty
                for _ in [1 .. total] do
                    let mutable nextoffline = registered_list.[offlinerand.Next(registered_list.Length)]
                    while cur_offline.Contains(nextoffline) || newset.Contains(nextoffline) do
                        nextoffline <- registered_list.[offlinerand.Next(registered_list.Length)]
                    server <! ("GoOffline", id, nextoffline, "", timestamp)
                    useraddress.[nextoffline] <! GoOffline
                    newset <- Set.add nextoffline newset

                for goonline in cur_offline do
                    server <! ("GoOnline", id, goonline, "",timestamp)

                // printerRef <! sprintf "new_set: %A" newset
                cur_offline <- Set.empty
                cur_offline <- newset
                // printerRef <! sprintf "new offline: %A" cur_offline
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, Offline)
            | AckOnline (uid)->
                useraddress.[uid] <! GoOnline
        | :? Tuple<string,string,string,string,string> as serverReply ->
            let (mtype,_,_,_,_) : Tuple<string,string,string,string,string> = downcast msg
            match mtype with 
                | "AckClientReg" ->
                    mailbox.Self <! RegisterUser(1)
                    system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, Offline)
                | "AckUserReg" ->
                    let (_,uid,msg,_,_) : Tuple<string,string,string,string,string> = downcast msg
                    printerRef <! msg
                    let mutable baseInterval = nusers/100
                    if baseInterval < 5 then
                        baseInterval <- 5            
                    useraddress.[uid] <! Ready(uid,clientslist,server,nusers,id,hashTagsList,(baseInterval*intervalmap.[uid]))
                | "AckOnline" ->
                  let (_,uid,_,_,_) : Tuple<string,string,string,string,string> = downcast msg
                  useraddress.[uid] <! GoOnline
                | _ -> ()
        | _ -> ()
        return! loop()
    }   
    loop()

// Start of the algorithm - spawn Boss, the delgator
let boss = spawn system "AdminActor" ClientAdminActor
boss <! Start(id, usersPerClient, clientCount, port)
system.WhenTerminated.Wait()|>ignore

