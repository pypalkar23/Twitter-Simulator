module RemoteMessages
open System
open FSharp.Json
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

let ClientRegisterPayload cid cliIp port =
    let payload = {
        operation = "ClientRegister"
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
        operation = "UserRegister"
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
        operation = "GoOnline"
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
        operation = "GoOffline"
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
        operation = "Follow"
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
        operation = "Tweet"
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

let Retweet cid user reqTime =
    let payload = {
        operation = "Retweet"
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

let QueryMentions cid userid mention reqTime =
    let payload = {
        operation = "QueryMentions"
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

let QueryHashtags cid userid tag reqTime =
    let payload = {
        operation = "QueryHashtags"
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

let AckUserRegPayload uid msg =
    let payload = {
        operation = "AckUserReg"
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
        operation = "AckOnline"
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
        operation = "AckClientReg"
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


(*
    "ClientRegister" -> let (_,cid,cliIP,port,_)
    UserRegister" (_,cid,userid,subscount,reqTime) 
    "GoOnline" - (_,cid,userid,_,reqTime)
    GoOffline" (_,cid,userid,_,reqTime)
    "Follow"(_,cid,userid,followingid,reqTime) 
    "Tweet" (_,cid,userid,twt,reqTime) 
    "ReTweet"(_,cid,userid,_,reqTime)
    "QueryMentions"(_,cid,uid,mention,reqTime)
    "QueryHashtags" (_,cid,uid,tag,reqTime)
*)

