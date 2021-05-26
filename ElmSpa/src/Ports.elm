port module Ports exposing
    ( log
    , logDebug
    , logError
    , logWarning
    )


port log : String -> Cmd msg


port logError : String -> Cmd msg


port logWarning : String -> Cmd msg


port logDebug : String -> Cmd msg
