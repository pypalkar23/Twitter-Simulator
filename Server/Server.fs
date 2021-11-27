
open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.IO
open RemoteMessages
open Akka.Serialization

let mutable serverip = "0.0.0.0"
let path = "Performance.log"

// Configuration
let configuration = 
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
                port = 8282
                hostname = %s
            }
        }" serverip)

let system = ActorSystem.Create("TwitterServer", configuration)

type InternalMessage =
    | PrintStatistics
    | ServiceStats of string*string
    | Start


type ClientMessages = 
    | AckUserReg of (string*string)

type UserMessages = 
    | Init of (IActorRef*IActorRef*IActorRef)
    | Register of (string*string*string*DateTime)
    | Follow of (string*string*string*DateTime)
    | Offline of (string*string*DateTime)
    | Online of (string*string*IActorRef*DateTime)
    | UpdateUserClientPrinters of (Map<string,ActorSelection>)
    | UpdateFeeds of (string*string*string*string*DateTime)
    | UsersPrint of (Map<string,string>*uint64*DateTime)

type RetweetMessages = 
    | InitRetweet of (IActorRef*IActorRef)
    | Retweet of (string*string*DateTime)
    | RetweetFeedTable of (string*string*string)
    | UpdateRetweetClientPrinters of (Map<string,ActorSelection>)

type ShowFeedMessages = 
    | ShowFeeds of (string*string*IActorRef)
    | UpdateFeedTable of (string*string*string)
    | UpdateShowFeedClientPrinters of (Map<string,ActorSelection>)

type MentionsMessages = 
    | InitMentions of (IActorRef)
    | MentionsRegister of (string*string)
    | ParseMentions of (string*string*string*DateTime)
    | UpdateMentionsClientPrinters of (Map<string,ActorSelection>)
    | QueryMentions of (string*string*string*DateTime)

type HashTagMessages = 
    | ParseHashTags of (string*string*string)
    | UpdateHashTagsClientPrinters of (Map<string,ActorSelection>)
    | QueryHashtags of (string*string*string*DateTime)

type TweetMessages = 
    | InitTweet of (IActorRef*IActorRef)
    | UpdateTweetsClientPrinters of (Map<string,ActorSelection>)
    | Tweet of (string*string*string*DateTime*IActorRef)
    | PrintTweetStats of (Map<string,Set<string>>*Map<string,string>*uint64)
    | IncTweet of (string)

let TweetActor (mailbox:Actor<_>) = 
    let mutable cprinters = Map.empty
    let mutable tweetCount = 0.0
    let mutable usersTweetCount = Map.empty
    let mutable hashTagActor:IActorRef = null
    let mutable usersActor:IActorRef = null
    let mutable twTotalTime = 0.0
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        let timestamp = DateTime.Now
        match message with
        | InitTweet(htactor,uactor) ->
            hashTagActor <- htactor
            usersActor <- uactor

        | UpdateTweetsClientPrinters(ob) ->
            cprinters <- ob

        | Tweet(cid, uid, twt, reqTime,boss) ->
            tweetCount <- tweetCount+1.0
            let mutable twCount = 0
            cprinters.[cid] <! sprintf "[%s][TWEET] %s" (timestamp.ToString()) twt

            if usersTweetCount.ContainsKey uid then 
                twCount <- usersTweetCount.[uid] + 1
                usersTweetCount <- Map.remove uid usersTweetCount
            usersTweetCount <- Map.add uid twCount usersTweetCount

            hashTagActor <! ParseHashTags(cid,uid,twt)
            usersActor <! UpdateFeeds(cid,uid,twt, "tweeted", DateTime.Now)
            twTotalTime <- twTotalTime + (timestamp.Subtract reqTime).TotalMilliseconds
            let averageTime = twTotalTime / tweetCount
            boss <! ServiceStats("Tweet",(averageTime |> string))

        | IncTweet(uid) ->
            if usersTweetCount.ContainsKey uid then 
                let twCount = (usersTweetCount.[uid] + 1)
                usersTweetCount <- Map.remove uid usersTweetCount  
                usersTweetCount <- Map.add uid twCount usersTweetCount  

        | PrintTweetStats(followings,reqStats,perf) ->
            File.WriteAllText(path, "")
            File.AppendAllText(path, ("\n"+timestamp.ToString()))
            File.AppendAllText(path, (sprintf "\nNumber of user requests handled per second = %u\n" perf))
            File.AppendAllText(path, "\nAverage time taken for service(s) in ms:")
            for stat in reqStats do
                File.AppendAllText(path, (sprintf "\n%s = %s" stat.Key stat.Value))
            let headers = "\n\nUserID\t#Followers\t#Tweets\n"
            File.AppendAllText(path, headers)
            for uid in followings do
                if usersTweetCount.ContainsKey uid.Key then
                    let stat = sprintf "%s\t%s\t%s\n" uid.Key (uid.Value.Count |> string) (usersTweetCount.[uid.Key] |> string)
                    File.AppendAllText(path, stat)                
        return! loop()
    }
    loop()

let RetweetActor (mailbox:Actor<_>) = 
    let mutable cprinters = Map.empty
    let mutable feedtable = Map.empty
    let mutable usersRand = Random()
    let mutable usersactor:IActorRef = null
    let mutable tweetactor:IActorRef = null
    let mutable reTweetCount = 0.0
    let mutable reTweetTime = 0.0
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        let timestamp = DateTime.Now
        match message with
        | InitRetweet(uactor,tactor) ->
            usersactor <- uactor
            tweetactor <- tactor

        | UpdateRetweetClientPrinters(ob) ->
            cprinters <- ob

        | RetweetFeedTable(uid,_,twt) ->
            let mutable listy = []
            if feedtable.ContainsKey uid then
                listy <- feedtable.[uid]
            listy  <- twt :: listy
            feedtable <- Map.remove uid feedtable
            feedtable <- Map.add uid listy feedtable

        | Retweet(cid,uid,reqTime) ->
            if feedtable.ContainsKey uid then   
                reTweetCount <- reTweetCount + 1.0
                let randTweet = feedtable.[uid].[usersRand.Next(feedtable.[uid].Length)]
                cprinters.[cid] <! sprintf "[%s][RE_TWEET] %s retweeted -> %s" (timestamp.ToString()) uid randTweet
                reTweetTime <- reTweetTime + (timestamp.Subtract reqTime).TotalMilliseconds
                let averageTime = reTweetTime / reTweetCount
                mailbox.Sender() <! ServiceStats("ReTweet",(averageTime |> string))
                // printfn "cnt %f, totaltime %f, avg %f" reTweetCount reTweetTime averageTime 
                usersactor <! UpdateFeeds(cid,uid,randTweet,"retweeted",DateTime.Now)
                tweetactor <! IncTweet(uid)                   
        return! loop()
    }
    loop()

let ShowfeedActor (mailbox:Actor<_>) = 
    let mutable cprinters = Map.empty
    let mutable feedtable = Map.empty
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        let timestamp = DateTime.Now
        match message with
        | UpdateShowFeedClientPrinters(ob) ->
            cprinters <- ob

        | ShowFeeds(cid, uid, cadmin) ->
            if feedtable.ContainsKey uid then
                let mutable feedsTop = ""
                let mutable fSize = 10
                let feedList:List<string> = feedtable.[uid]
                if feedList.Length < 10 then
                    fSize <- feedList.Length
                for i in [0..(fSize-1)] do
                    feedsTop <- "\n" + feedtable.[uid].[i]
                cprinters.[cid] <! sprintf "[%s][ONLINE] User %s is online..Feeds -> %s" (timestamp.ToString()) uid feedsTop
            else
                cprinters.[cid] <! sprintf "[%s][ONLINE] User %s is online..No feeds yet!!!" (timestamp.ToString()) uid
            cadmin <! ("AckOnline", uid, "", "", "")

        | UpdateFeedTable(uid, _, twt) ->
            let mutable listy = []
            if feedtable.ContainsKey uid then
                listy <- feedtable.[uid]
            listy  <- twt :: listy
            feedtable <- Map.remove uid feedtable
            feedtable <- Map.add uid listy feedtable
        return! loop()
    }
    loop()


let HashTagsActor (mailbox:Actor<_>) = 
    let mutable cprinters = Map.empty
    let mutable hashtagsMap = Map.empty
    let mutable queryHTTotalTime = 1.0
    let mutable queryHTCount = 1.0
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        let timestamp = DateTime.Now
        match message with
        | UpdateHashTagsClientPrinters(ob) ->
            cprinters <- ob

        | ParseHashTags(_,_,twt) ->
            let parsed = twt.Split ' '
            for parse in parsed do
                if parse.[0] = '#' then
                    let parsedTag = parse.[1..(parse.Length-1)]
                    if not (hashtagsMap.ContainsKey parsedTag) then
                        hashtagsMap <- Map.add parsedTag List.empty hashtagsMap
                    let mutable tList = hashtagsMap.[parsedTag]
                    tList <- twt :: tList
                    hashtagsMap <- Map.remove parsedTag hashtagsMap
                    hashtagsMap <- Map.add parsedTag tList hashtagsMap
                    ignore()

        | QueryHashtags(cid,uid,hashtag,reqTime) ->
            if cprinters.ContainsKey cid then
                queryHTCount <- queryHTCount+1.0
                if hashtagsMap.ContainsKey hashtag then
                    let mutable hSize = 10
                    if (hashtagsMap.[hashtag].Length < 10) then
                        hSize <- hashtagsMap.[hashtag].Length
                    let mutable tagsstring = ""
                    for i in [0..(hSize-1)] do
                        tagsstring <- "\n" + hashtagsMap.[hashtag].[i]
                    cprinters.[cid] <! sprintf "[%s][QUERY_HASHTAG] by user %s: Recent 10(Max) tweets for hashTag #%s ->%s" (timestamp.ToString()) uid hashtag tagsstring
                else    
                    cprinters.[cid] <! sprintf "[%s][QUERY_HASHTAG] by user %s: No tweets for hashTag #%s" (timestamp.ToString()) uid hashtag
                
                queryHTTotalTime <- queryHTTotalTime + (timestamp.Subtract reqTime).TotalMilliseconds
                let averageHTTime = (queryHTTotalTime / queryHTCount)
                // printfn "cnt %f, totaltime %f, avg %f" queryHTCount queryHTTotalTime averageHTTime
                mailbox.Sender() <! ServiceStats("QueryHashTag",(averageHTTime |> string))
        return! loop()
    }
    loop()

let MentionsActor (mailbox:Actor<_>) = 
    let mutable users = Set.empty
    let mutable tweetActor:IActorRef = null
    let mutable mentionsMap = Map.empty
    let mutable cprinters = Map.empty
    let mutable queryTotalTime = 1.0
    let mutable queryCount = 1.0
    let rec loop () = actor {
        let! message = mailbox.Receive() 
        let timestamp = DateTime.Now
        match message with
        | InitMentions(tactor) ->
            tweetActor <- tactor

        | UpdateMentionsClientPrinters(ob) ->
            cprinters <- ob

        | MentionsRegister(_,uid) ->
            users <- Set.add uid users
            mentionsMap <- Map.add uid List.empty mentionsMap

        | ParseMentions(cid, uid, twt,reqTime) ->
            if users.Contains uid then
                let parsed = twt.Split ' '
                let mutable mentionFound = false
                for parse in parsed do
                    if parse.[0] = '@' then
                        mentionFound <- true
                        let parsedMention = parse.[1..(parse.Length-1)]
                        if users.Contains parsedMention then
                            let mutable mList = mentionsMap.[parsedMention]
                            mList <- twt :: mList
                            mentionsMap <- Map.remove parsedMention mentionsMap
                            mentionsMap <- Map.add parsedMention mList mentionsMap
                            tweetActor <! Tweet(cid, uid, twt,reqTime,mailbox.Sender())
                if not mentionFound then 
                    tweetActor <! Tweet(cid, uid, twt,reqTime,mailbox.Sender())

        | QueryMentions(cid,uid,mention,reqTime) ->
            if cprinters.ContainsKey cid then
                queryCount <- queryCount+1.0
                if mentionsMap.ContainsKey mention then
                    let mutable mSize = 10
                    if (mentionsMap.[mention].Length < 10) then
                        mSize <- mentionsMap.[mention].Length
                    let mutable tweetsstring = ""
                    for i in [0..(mSize-1)] do
                        tweetsstring <- "\n" + mentionsMap.[mention].[i]
                    cprinters.[cid] <! sprintf "[%s][QUERY_MENTION] by user %s: Recent 10(Max) tweets for user @%s ->%s" (timestamp.ToString()) uid mention tweetsstring
                else
                    cprinters.[cid] <! sprintf "[%s][QUERY_MENTION] by user %s: No tweets for user @%s" (timestamp.ToString()) uid mention
                queryTotalTime <- queryTotalTime + (timestamp.Subtract reqTime).TotalMilliseconds
                let averageTime = queryTotalTime / queryCount
                mailbox.Sender() <! ServiceStats("QueryMentions",(averageTime |> string))
        return! loop()
    }
    loop()

let ServerUsersActor (mailbox:Actor<_>) = 
    let mutable retweetactor:IActorRef = null
    let mutable tweetactor:IActorRef = null
    let mutable showfeedactor:IActorRef = null
    let mutable cprinters = Map.empty
    let mutable users = Set.empty
    let mutable followings = Map.empty
    let mutable nonactiveusers = Set.empty
    let mutable subsrank = Map.empty
    let mutable userServiceCount = 1.0
    let mutable followTime = 1.0
    let rec loop () = actor {
        let! message = mailbox.Receive()  
        let timestamp = DateTime.Now
        match message with
        | Init(rtactor, feedactor, tactor) ->
            retweetactor <- rtactor
            showfeedactor <- feedactor
            tweetactor <- tactor
            ignore()

        | UpdateUserClientPrinters(ob) ->
            cprinters <- ob
            ignore()

        | Register(_, uid, scount,reqTime) ->
            users <- Set.add uid users
            subsrank <- Map.add uid (scount |> int) subsrank
            followings <- Map.add uid Set.empty followings
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds
            userServiceCount <- userServiceCount + 1.0

        | Offline(cid, uid, reqTime) ->
            nonactiveusers <- Set.add uid nonactiveusers
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds
            userServiceCount <- userServiceCount + 1.0
            cprinters.[cid] <! sprintf "[%s][OFFLINE] User %s is going offline" (timestamp.ToString()) uid

        | Online(cid, uid, cadmin, reqTime) ->
            nonactiveusers <- Set.remove uid nonactiveusers
            showfeedactor <! ShowFeeds(cid, uid, cadmin)
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds
            userServiceCount <- userServiceCount + 1.0

        | Follow(cid, uid, fid, reqTime) ->
            userServiceCount <- userServiceCount + 1.0
            if followings.ContainsKey fid && not (followings.[fid].Contains uid) && followings.[fid].Count < subsrank.[fid] then
                let mutable st = followings.[fid]
                st <- Set.add uid st
                followings <- Map.remove fid followings
                followings <- Map.add fid st followings
                cprinters.[cid] <! sprintf "[%s][FOLLOW] User %s started following %s" (timestamp.ToString()) uid fid
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds

        | UpdateFeeds(_,uid,twt,msg, reqTime) ->
            userServiceCount <- userServiceCount + 1.0
            for id in followings.[uid] do
                showfeedactor <! UpdateFeedTable(id, uid, twt) 
                retweetactor <! RetweetFeedTable(id, uid, twt)
                if not (nonactiveusers.Contains id) then
                    let splits = id.Split '_'
                    let sendtoid = splits.[0]
                    if msg = "tweeted" then
                        cprinters.[sendtoid] <! sprintf "[%s][NEW_FEED] For User: %s -> %s" (timestamp.ToString()) id twt    
                    else
                        cprinters.[sendtoid] <! sprintf "[%s][NEW_FEED] For User: %s -> %s %s - %s" (timestamp.ToString()) id uid msg twt
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds

        | UsersPrint(stats, perf, reqTime) -> 
            userServiceCount <- userServiceCount + 1.0
            tweetactor <! PrintTweetStats(followings,stats,perf)
            followTime <- followTime + (timestamp.Subtract reqTime).TotalMilliseconds 

        let averageTime = followTime / userServiceCount
        mailbox.Sender() <! ServiceStats("Follow/Offline/Online",(averageTime |> string))         
        return! loop()
    }
    loop()

let ServerRequestsHandler (mailbox:Actor<_>) = 
    let mutable hashtagactor:IActorRef = null
    let mutable tweetactor:IActorRef = null
    let mutable mentionsactor:IActorRef = null
    let mutable usersactor:IActorRef = null
    let mutable retweetactor:IActorRef = null
    let mutable showfeedactor:IActorRef = null
    let mutable clientprinters = Map.empty
    let mutable requests = 0UL
    let mutable starttime = DateTime.Now
    let mutable reqStats = Map.empty
    let sendToAllActors ob = 
        hashtagactor <! UpdateHashTagsClientPrinters(ob)
        tweetactor <! UpdateTweetsClientPrinters(ob)
        mentionsactor <! UpdateMentionsClientPrinters(ob)
        usersactor <! UpdateUserClientPrinters(ob)
        retweetactor <! UpdateRetweetClientPrinters(ob)
        showfeedactor <! UpdateShowFeedClientPrinters(ob)

    let rec loop () = actor {
        let! (message:obj) = mailbox.Receive()
        //let (mtype,_,_,_,_) : Tuple<string,string,string,string,DateTime> = downcast message 
        let timestamp = DateTime.Now
        match message with
        | :? Tuple<string,string,string,string,DateTime> as generalReply ->
            let (mtype,_,_,_,_) : Tuple<string,string,string,string,DateTime> = downcast message
            match mtype with
            | "ClientRegister" -> 
                let (_,cid,cliIP,port,_) : Tuple<string,string,string,string,DateTime> = downcast message 
                requests <- requests + 1UL
                let clientp = system.ActorSelection(sprintf "akka.tcp://TwitterClient@%s:%s/user/Printer" cliIP port)
                clientprinters <- Map.add cid clientp clientprinters
                sendToAllActors clientprinters
                mailbox.Sender() <! ("AckClientReg",sprintf "[%s][CLIENT_REGISTER] Client %s registered with server" (timestamp.ToString()) cid,"","","")
            | "UserRegister" ->
                let (_,cid,userid,subscount,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message 
                usersactor <! Register(cid, userid, subscount,reqTime)
                mentionsactor <! MentionsRegister(cid, userid)
                requests <- requests + 1UL
                let st = sprintf "[%s][USER_REGISTER] User %s registered with server" (timestamp.ToString()) userid
                mailbox.Sender() <! ("AckUserReg",userid,st,"","")
            | "GoOnline" ->
                let (_,cid,userid,_,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message 
                requests <- requests + 1UL
                usersactor <! Online(cid, userid, mailbox.Sender(),reqTime)
            | "GoOffline" ->
                let (_,cid,userid,_,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message 
                requests <- requests + 1UL
                usersactor <! Offline(cid, userid,reqTime)
            | "Follow" ->
                let (_,cid,userid,followingid,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message 
                requests <- requests + 1UL
                usersactor <! Follow(cid, userid, followingid, reqTime)
            | "Tweet" ->
                let (_,cid,userid,twt,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message 
                requests <- requests + 1UL
                mentionsactor <! ParseMentions(cid,userid,twt,reqTime)
            | "ReTweet" ->
                let (_,cid,userid,_,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message   
                requests <- requests + 1UL
                retweetactor <! Retweet(cid,userid,reqTime)
            | "QueryMentions" ->
                let (_,cid,uid,mention,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message  
                requests <- requests + 1UL
                mentionsactor <! QueryMentions(cid,uid,mention,reqTime)
            | "QueryHashtags" ->
                let (_,cid,uid,tag,reqTime) : Tuple<string,string,string,string,DateTime> = downcast message  
                requests <- requests + 1UL
                hashtagactor <! QueryHashtags(cid,uid,tag,reqTime)
            (*| "ServiceStats" ->
                //done
                let (_,_,key,value,_) : Tuple<string,string,string,string,DateTime> = downcast message 
                if key <> "" then
                    if reqStats.ContainsKey key then
                        reqStats <- Map.remove key reqStats
                    reqStats <- Map.add key value reqStats
            | "PrintStatistics" ->
                //done
                let mutable perf = 0UL
                let timediff = (DateTime.Now-starttime).TotalSeconds |> uint64
                if requests > 0UL then
                    perf <- requests/timediff
                    usersactor <! UsersPrint(reqStats, perf, DateTime.Now)  
                    printfn "Server uptime = %u seconds, requests served = %u, Avg requests served = %u per second" timediff requests perf
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, PrintStatistics)*)
            | _ -> ignore()
        | :? InternalMessage as internalMsg ->
            match internalMsg with
            | PrintStatistics ->
                let mutable perf = 0UL
                let timediff = (DateTime.Now-starttime).TotalSeconds |> uint64
                if requests > 0UL then
                    perf <- requests/timediff
                    usersactor <! UsersPrint(reqStats, perf, DateTime.Now)  
                    printfn "Server uptime = %u seconds, requests served = %u, Avg requests served = %u per second" timediff requests perf
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, PrintStatistics)
            | ServiceStats(key,value) ->
              if key <> "" then
                    if reqStats.ContainsKey key then
                        reqStats <- Map.remove key reqStats
                    reqStats <- Map.add key value reqStats 
            | Start ->
                printfn "Start!!"      
                hashtagactor <- spawn system (sprintf "HashTagsActor") HashTagsActor
                tweetactor <- spawn system (sprintf "TweetActor") TweetActor
                mentionsactor <- spawn system (sprintf "MentionsActor") MentionsActor
                usersactor <- spawn system (sprintf "UsersActor") ServerUsersActor
                retweetactor <- spawn system (sprintf "RetweetActor") RetweetActor
                showfeedactor <- spawn system (sprintf "ShowfeedActor") ShowfeedActor
                //send actors the needed info
                usersactor <! Init(retweetactor, showfeedactor, tweetactor)
                tweetactor <! InitTweet(hashtagactor,usersactor)
                mentionsactor <! InitMentions(tweetactor)

                retweetactor <! InitRetweet(usersactor,tweetactor)
                starttime <- DateTime.Now
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, PrintStatistics)
        (*| :? RemoteMessage as rMsg ->
            match rMsg with 
            | ClientRegister(cid,cliIP,port) ->
                printfn "In the new client Register Created"
                requests <- requests + 1UL
                let clientp = system.ActorSelection(sprintf "akka.tcp://TwitterClient@%s:%s/user/Printer" cliIP port)
                clientprinters <- Map.add cid clientp clientprinters
                sendToAllActors clientprinters
                mailbox.Sender() <! ("AckClientReg",sprintf "[%s][CLIENT_REGISTER] Client %s registered with server" (timestamp.ToString()) cid,"","","")*)
        | _ ->()
        
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =

    serverip <- argv.[0] |> string

    // Start of the algorithm - spawn Boss, the delgator
    let serverMainRef = spawn system "ServerRequestsHandler" ServerRequestsHandler
    serverMainRef <! Start
    system.WhenTerminated.Wait()

    0