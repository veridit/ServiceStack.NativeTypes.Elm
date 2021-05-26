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
    | NotFoundRoute


{-| Translate from url to route.
Notice that this must be kept in sync with IndexService.cs->PathPrefixWhiteList
-}
routePath : Route -> String
routePath route =
    case route of
        FrontRoute ->
            absolute [] []

        NotFoundRoute ->
            absolute [] []


{-| Navigate and preserve history.
-}
goTo : Key -> Route -> Cmd msg
goTo key route =
    routePath route
        |> Browser.Navigation.pushUrl key


matchers : Parser (Route -> a) a
matchers =
    oneOf
        [ map FrontRoute top
        ]


parseUrl : Url -> Route
parseUrl url =
    parse matchers url
        |> Maybe.withDefault NotFoundRoute
