module Model exposing (Flags, Model, Page(..))

import Browser.Navigation as Navigation
import Ports exposing (ClientInfo)
import Routing
import Time


type alias Model =
    { navigationKey : Navigation.Key
    , route : Routing.Route
    , page : Page
    , timeZone : Time.Zone
    }


type Page
    = FrontPage
    | ClientInfoPage (Maybe ClientInfo)


type alias Flags =
    {}
