module RemoteMessages
open System
open FSharp.Json

[<Literal>]
let clientRegOp = "ClientRegister"

[<Literal>]
let userRegOp ="UserRegister"

[<Literal>]
let toggleStateOnlineOp = "GoOnline"

[<Literal>]
let toggleStateOfflineOp = "GoOffline"

[<Literal>]
let followOp = "Follow"

[<Literal>]
let tweetOp = "Tweet"

[<Literal>]
let retweetOp = "ReTweet"

[<Literal>]
let searchByMentionsOp = "QueryMentions"

[<Literal>]
let searchByHashtagsOp = "QueryHashtags"

[<Literal>]
let userRegisterAckOp = "AckUserReg"

[<Literal>]
let clientRegisterAckOp = "AckClientReg"

[<Literal>]
let userOnlineAckOp = "AckOnline"

[<Literal>]
let TweetUserAction = "Tweet"

[<Literal>]
let RetweetUserAction = "ReTweet"

[<Literal>]
let TweetWithHashtagsUserAction = "Tweet with only hashtags"

[<Literal>]
let TweetMentionsAndHashtags = "Tweet with mentions and hashtags"

[<Literal>]
let FollowUserAction = "Follow"

[<Literal>]
let QueryHashtagsUserAction = "QueryHashtags"

[<Literal>]
let QueryMentionsUserAction = "QueryMentions"

type RemoteMessage = {
    operation: string
    cid:string
    userid:string option
    cliIp: string option
    port:string option
    followingid: string option
    twt: string option
    mention: string option
    tag: string option
    subscount : string option
    reqTime: DateTime option
    msg : string option
}

// ----- (Client -> Server) Messages Payloads Start-----
let ClientRegisterPayload cid cliIp port =
    let payload = {
        operation = clientRegOp
        cid = cid
        port = Some(port)
        cliIp = Some(cliIp)
        followingid = None
        twt = None
        mention = None
        tag = None
        subscount = None
        reqTime = None
        userid = None
        msg = None
    }
    Json.serialize payload 

let UserRegisterPayload cid userid subscount reqTime =
    let payload={
        operation = userRegOp
        cid = cid
        userid = Some(userid)
        subscount = Some(subscount)
        reqTime = Some(reqTime)
        port = None
        cliIp = None
        followingid = None
        twt = None
        mention = None
        tag = None   
        msg = None     
    }
    Json.serialize payload 

let OnlinePayload cid userid reqTime =
    let payload={
        operation = toggleStateOnlineOp
        cid = cid
        userid = Some(userid)
        reqTime = Some(reqTime)
        subscount = None
        port = None
        cliIp = None
        followingid = None
        twt = None
        mention = None
        tag = None        
        msg = None
    }
    Json.serialize payload 

let OfflinePayload cid userid reqTime =
    let payload={
        operation = toggleStateOfflineOp
        cid = cid
        userid = Some(userid)
        reqTime = Some(reqTime)
        subscount = None
        port = None
        cliIp = None
        followingid = None
        twt = None
        mention = None
        tag = None     
        msg = None   
    }
    Json.serialize payload 

let FollowPayload cid userid followingid reqTime =
    let payload={
        operation = followOp
        cid = cid
        userid = Some(userid)
        followingid = Some(followingid)
        reqTime = Some(reqTime)
        subscount = None
        port = None
        cliIp = None
        twt = None
        mention = None
        tag = None   
        msg = None     
    }
    Json.serialize payload 

let TweetPayload cid userid twt reqTime =
    let payload = {
        operation = tweetOp
        cid = cid
        userid = Some(userid)
        twt = Some(twt)
        reqTime = Some(reqTime)
        followingid = None
        subscount = None
        port = None
        cliIp = None 
        mention = None
        tag = None 
        msg = None
    }
    Json.serialize payload 

let RetweetPayload cid user reqTime =
    let payload = {
        operation = retweetOp
        cid = cid
        userid = Some(user)
        reqTime = Some(reqTime)
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None 
        mention = None
        tag = None 
        msg = None
    }
    Json.serialize payload 

let QueryMentionsPayload cid userid mention reqTime =
    let payload = {
        operation = searchByMentionsOp
        cid = cid
        userid = Some(userid)
        mention = Some(mention)
        reqTime = Some(reqTime)
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None 
        tag = None 
        msg = None
    }
    Json.serialize payload 

let QueryHashtagsPayload cid userid tag reqTime =
    let payload = {
        operation = searchByHashtagsOp
        cid = cid
        userid = Some(userid)
        tag = Some(tag)
        reqTime = Some(reqTime)
        mention = None
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None 
        msg = None
    }
    Json.serialize payload 
// ----- (Client -> Server) Messages Payloads End -----

// ----- (Server -> Client) Messages Payloads Start -----
let AckUserRegPayload uid msg =
    let payload = {
        operation = userRegisterAckOp
        userid = Some(uid)
        msg = Some(msg)
        cid = ""
        tag = None
        reqTime = None
        mention = None
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None 
    }

    Json.serialize payload


let AckOnlinePayload uid =
    let payload = {
        operation = userOnlineAckOp
        userid = Some(uid)
        msg = None
        cid = ""
        tag = None
        reqTime = None
        mention = None
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None 
    }

    Json.serialize payload

let AckClientRegPayload =
    let payload = {
        operation = clientRegisterAckOp
        userid = None
        msg = None
        cid = ""
        tag = None
        reqTime = None
        mention = None
        twt = None
        followingid = None
        subscount = None
        port = None
        cliIp = None
    }

    Json.serialize payload
// ----- (Server -> Client) Messages Payloads End -----

