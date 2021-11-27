module RemoteMessages
open System

type RemoteMessage =
    | ClientRegister of string*string*string


//("ClientRegister",id,local_ip,port,timestamp)