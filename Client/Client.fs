
open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open FSharp.Data
open System.IO
open FSharp.Json
open RemoteMessages

[<Literal>]
let resolutionFolder = __SOURCE_DIRECTORY__
type configJson = JsonProvider<"config.json", ResolutionFolder = resolutionFolder>
let values = configJson.GetSample()

let args = Environment.GetCommandLineArgs()

//config read from the file
let local_ip = values.LocalIp
let id = args.[1] |> string
let port = (int(id) + values.Port) |> string
let usersPerClient = values.UserCount
let clientCount = values.ClientCount
let serverip = values.ServerIp

printfn "port = %s" port
// Configuration
let config = 
    ConfigurationFactory.ParseString(
        sprintf @"akka {            
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote.helios.tcp {
                transport-protocol = tcp
                port = %s
                hostname = %s
            }
    }" port local_ip)

let system = ActorSystem.Create("TwitterClient", config)

type UserMessages = 
    | Ready of (string*list<string>*ActorSelection*int*string*List<string>*int)
    | GoOnline
    | GoOffline
    | Action
    | ActionTweet

type BossMessage = 
    | Start of (string*int*int*string)
    | RegisterUser of (int)
    | Offline

type FollowMessages = 
    | Init of (list<string>*int)

// Printer Actor - Prints on console & logs in the logfile
let path = sprintf "Client%s.log" id
File.Create(path) |> ignore
printfn "%s" path

let printerActor (mailbox:Actor<_>) = 
    let rec loop () = actor {
        let! (message:obj) = mailbox.Receive()
        printfn "%A" message
        File.AppendAllText(path, sprintf "\n%A" message)
        return! loop()
    }
    loop()

let printerRef = spawn system "Printer" printerActor


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
                let actions = [RemoteMessages.FollowUserAction; RemoteMessages.QueryHashtagsUserAction; RemoteMessages.QueryMentionsUserAction]
                let actionsrand = Random()
                let act = actions.[actionsrand.Next(actions.Length)]
                match act with
                | RemoteMessages.FollowUserAction ->
                    let mutable fUser = [1 .. usersCount].[followRand.Next(usersCount)] |> string
                    let mutable randomClientId = clientList.[clientRand.Next(clientList.Length)]
                    let mutable followUser = sprintf "%s_%s" randomClientId fUser
                    while followUser = myId do 
                        fUser <- [1 .. usersCount].[followRand.Next(usersCount)] |> string
                        followUser <- sprintf "%s_%s" randomClientId fUser 
                    server <! FollowPayload cliId myId followUser DateTime.Now
                | RemoteMessages.QueryHashtagsUserAction ->
                    let hashTag = topHashTags.[htagRandReq.Next(topHashTags.Length)]
                    server <! QueryHashtagsPayload cliId myId hashTag DateTime.Now
                | RemoteMessages.QueryMentionsUserAction ->
                    let mutable mUser = [1 .. usersCount].[mentionsRandReq.Next(usersCount)] |> string
                    let mutable randomClientId = clientList.[clientRand.Next(clientList.Length)]
                    let mutable mentionsUser = sprintf "%s_%s" randomClientId mUser
                    server <! QueryMentionsPayload cliId myId mentionsUser DateTime.Now
                | _ ->ignore()
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0), mailbox.Self, Action)

        | ActionTweet ->
            if isOnline then
                let actions = [RemoteMessages.TweetWithHashtagsUserAction; RemoteMessages.TweetUserAction; RemoteMessages.RetweetUserAction; RemoteMessages.TweetMentionsAndHashtags]
                let chooseRandomAction = Random()
                let act = actions.[chooseRandomAction.Next(actions.Length)]
                match act with
                | RemoteMessages.TweetUserAction ->
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted -> tweet_%d" myId tweetCount
                    server <! TweetPayload cliId myId tweetMsg DateTime.Now
                | RemoteMessages.RetweetUserAction ->
                    server <! RetweetPayload cliId myId DateTime.Now
                | RemoteMessages.TweetWithHashtagsUserAction ->
                    let hashTag = topHashTags.[htagRand.Next(topHashTags.Length)]
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted -> tweet_%d with hashtag #%s" myId tweetCount hashTag
                    server <! TweetPayload cliId myId tweetMsg DateTime.Now
                | RemoteMessages.TweetMentionsAndHashtags ->
                    let mutable mUser = [1 .. usersCount].[mentionsRand.Next(usersCount)] |> string
                    let mutable randclid = clientList.[clientRand.Next(clientList.Length)]
                    let mutable mentionsUser = sprintf "%s_%s" randclid mUser
                    while mentionsUser = myId do 
                        mUser <- [1 .. usersCount].[mentionsRand.Next(usersCount)] |> string
                        mentionsUser <- sprintf "%s_%s" randclid mUser 
                    let hashTag = topHashTags.[htagRand.Next(topHashTags.Length)]
                    tweetCount <- tweetCount+1
                    let tweetMsg = sprintf "%s tweeted tweet_%d with hashtag #%s and mentioned @%s" myId tweetCount hashTag mentionsUser
                    server <! TweetPayload cliId myId tweetMsg DateTime.Now
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
    let mutable port = "8282"
    let mutable clientslist = []
    let mutable cur_offline = Set.empty
    let mutable registered_list = []
    let mutable useraddress = Map.empty
    let mutable intervalmap = Map.empty
    let mutable usersList = []
    let mutable subsrank = Map.empty
    let server = system.ActorSelection(
                   sprintf "akka.tcp://TwitterServer@%s:%s/user/ServerRequestsHandler" serverip port)
    
    let hashTagsList = ["boosters";"UF";"covid19";"omicron";"delta";"iamvaccinated";"giveaway";"contest";
                        "blacklivesmatter";"freedom";"cryptocurrency";"ipl2021";"happybirthday";"pitrudevsaurakshnam";
                        "2021";"farmlaws";"internationalmensday";"eeSalaCupNamde"; "tejaMainHu";"monya";"todamjodam";
                        "bagwati";"gogators";"blackfriday";"tech";"womeninstem";"pottermore";"photography";
                        "mondaymotivation";"ootd";"vegan";"traveltuesday";"thanksgiving";"taimur";"devdutt";"mandiryahibanega"]

    let rec loop () = actor {
        let! (msg:obj) = mailbox.Receive() 
        let sender = mailbox.Sender()
        match msg with
        | :? BossMessage as bm ->
            match bm with 
            | Start(idParam, numUsers, numClients, clientPortParam) ->
                let timestamp = DateTime.Now
                id <- idParam
                nusers <- numUsers
                nclients <- numClients
                port <- clientPortParam
                printerRef <! sprintf "Starting twitter client with Id: %s " id
                let mutable usersarr = [| 1 .. nusers |]
                let rand = Random()
                
                let swap (a: _[]) x y =
                    let tmp = a.[x]
                    a.[x] <- a.[y]
                    a.[y] <- tmp
                
                let shuffle a =
                    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a

                shuffle usersarr
                usersList <- usersarr |> Array.toList
                for i in [1 .. nusers] do
                    let userkey = usersarr.[i-1] |> string
                    subsrank <- Map.add (sprintf "%s_%s" id userkey) ((nusers-1)/i) subsrank
                    intervalmap <- Map.add (sprintf "%s_%s" id userkey) i intervalmap

                server <! ClientRegisterPayload id local_ip port
                for i in [1 .. nclients] do
                    let istr = i |> string
                    clientslist <- istr :: clientslist

            | RegisterUser(nextId) ->
                let mutable numcurid = nextId |> int32
                let mutable curid = sprintf "%s_%s" id (usersList.[numcurid-1] |> string) 
                let ref = spawn system (sprintf "User_%s" curid) UserActor
                useraddress <- Map.add curid ref useraddress
                let subsstr = subsrank.[curid] |> string
                server <! UserRegisterPayload id curid subsstr DateTime.Now
                registered_list <- curid :: registered_list
                if numcurid < nusers then
                    numcurid <- numcurid+1
                    let stnumcurid = numcurid
                    system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(50.0), mailbox.Self, RegisterUser(numcurid))

            | Offline ->
                let chooseRandomOffline = Random()
                let mutable total = registered_list.Length
                total <- (25*total)/100
                let mutable newset = Set.empty
                for _ in [1 .. total] do
                    let mutable nextoffline = registered_list.[chooseRandomOffline.Next(registered_list.Length)]
                    while cur_offline.Contains(nextoffline) || newset.Contains(nextoffline) do
                        nextoffline <- registered_list.[chooseRandomOffline.Next(registered_list.Length)]
                    server <! OfflinePayload id nextoffline DateTime.Now
                    useraddress.[nextoffline] <! GoOffline
                    newset <- Set.add nextoffline newset

                for goonline in cur_offline do
                    server <! OnlinePayload id goonline DateTime.Now

                cur_offline <- Set.empty
                cur_offline <- newset
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, Offline)

        | :? string as remoteReply ->
            let jsonMsg = Json.deserialize<RemoteMessage> remoteReply
            let replyType = jsonMsg.operation
            
            match replyType with 
            | RemoteMessages.clientRegisterAckOp ->
                mailbox.Self <! RegisterUser(1)
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, Offline)

            | RemoteMessages.userRegisterAckOp ->
                let uid = jsonMsg.userid.Value 
                let msg = jsonMsg.msg.Value
                printerRef <! msg
                let mutable baseInterval = nusers/100
                if baseInterval < 5 then
                    baseInterval <- 5            
                useraddress.[uid] <! Ready(uid,clientslist,server,nusers,id,hashTagsList,(baseInterval*intervalmap.[uid]))

            | RemoteMessages.userOnlineAckOp ->
                  let uid = jsonMsg.userid.Value
                  useraddress.[uid] <! GoOnline
            
            | _ -> ()

        | _ -> ()
        return! loop()
    }   
    loop()


[<EntryPoint>]
let main argv =

    let boss = spawn system "AdminActor" ClientAdminActor
    boss <! Start(id, usersPerClient, clientCount, port)
    system.WhenTerminated.Wait()|>ignore

    0