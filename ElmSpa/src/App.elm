module App exposing (main)

import Browser
import Model exposing (Flags, Model)
import Update exposing (Msg(..), init, subscriptions, update)
import View exposing (view)


main : Program Flags Model Msg
main =
    Browser.application
        { init = init
        , view = view
        , update = update
        , onUrlRequest = OnUrlRequest
        , onUrlChange = OnUrlChange
        , subscriptions = subscriptions
        }
