port module Ports exposing
    ( ClientInfo
    , getClientInfo
    , getClientInfoReply
    , log
    , logDebug
    , logError
    , logWarning
    )


type alias ClientInfo =
    { operatingSystem : String
    , userAgent : String
    , width : Int
    , height : Int
    , language : String
    }


port getClientInfo : () -> Cmd msg


port getClientInfoReply : (ClientInfo -> msg) -> Sub msg


port log : String -> Cmd msg


port logError : String -> Cmd msg


port logWarning : String -> Cmd msg


port logDebug : String -> Cmd msg
