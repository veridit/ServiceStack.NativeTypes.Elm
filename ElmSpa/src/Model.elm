module Model exposing (Flags, Model)

import Browser.Navigation as Navigation
import Dtos
import Routing exposing (Route)


type alias Model =
    { navKey : Navigation.Key
    , route : Route
    , name : String
    , helloResponse : Maybe Dtos.HelloResponse
    }


type alias Flags =
    {}
