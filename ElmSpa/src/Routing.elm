module Routing exposing
    ( Route(..)
    , goTo
    , matchers
    , parseUrl
    , routePath
    )

import Browser.Navigation exposing (Key)
import Url exposing (Url)
import Url.Builder exposing (absolute)
import Url.Parser exposing ((</>), (<?>), Parser, map, oneOf, parse, s, top)


type Route
    = FrontRoute
    | ClientInfoRoute
    | NotFoundRoute


client_info_path : String
client_info_path =
    "client_info"


{-| Translate from url to route.
Notice that this must be kept in sync with IndexService.cs->PathPrefixWhiteList
-}
routePath : Route -> String
routePath route =
    case route of
        FrontRoute ->
            absolute [] []

        ClientInfoRoute ->
            absolute [ client_info_path ] []

        NotFoundRoute ->
            absolute [] []


{-| Navigate and preserve history.
-}
goTo : Key -> Route -> Cmd msg
goTo key route =
    routePath route
        |> Browser.Navigation.pushUrl key


matchers : Url -> Parser (Route -> a) a
matchers url =
    oneOf
        [ map FrontRoute top
        , map ClientInfoRoute (s client_info_path)
        ]


parseUrl : Url -> Route
parseUrl url =
    parse (matchers url) url
        |> Maybe.withDefault NotFoundRoute
