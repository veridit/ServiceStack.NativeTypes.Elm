module Update exposing (Msg(..), init, subscriptions, update)

import Browser exposing (UrlRequest(..))
import Browser.Navigation as Navigation
import Model exposing (Flags, Model, Page(..))
import Ports exposing (ClientInfo)
import Routing exposing (Route(..))
import Task
import Time
import Url exposing (Url)


type Msg
    = OnUrlChange Url
    | OnUrlRequest UrlRequest
    | NavigateTo Route
    | ClientInfoReply ClientInfo
    | GetTimeZone
    | GetTimeZoneReply Time.Zone


init : Flags -> Url -> Navigation.Key -> ( Model, Cmd Msg )
init _ url navigationKey =
    { navigationKey = navigationKey
    , page = FrontPage
    , route = FrontRoute
    , timeZone = Time.utc
    }
        |> routeTo url


getTimeZone : (Time.Zone -> msg) -> Cmd msg
getTimeZone callback =
    Time.here
        |> Task.perform callback


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        OnUrlRequest urlRequest ->
            case urlRequest of
                Internal newUrl ->
                    ( model, Navigation.pushUrl model.navigationKey <| Url.toString newUrl )

                External newUrl ->
                    ( model, Navigation.load newUrl )

        OnUrlChange newUrl ->
            routeTo newUrl model

        ClientInfoReply clientInfo ->
            case model.page of
                ClientInfoPage _ ->
                    ( { model | page = ClientInfoPage <| Just clientInfo }, Cmd.none )

                _ ->
                    ( model, Ports.logError "ClientInfoReply unexpected page state" )

        NavigateTo route ->
            if route == model.route then
                ( model, Ports.logWarning "Navigation to same route" )

            else
                ( model, Routing.goTo model.navigationKey route )

        GetTimeZone ->
            ( model, getTimeZone GetTimeZoneReply )

        GetTimeZoneReply zone ->
            ( { model | timeZone = zone }, Cmd.none )


routeTo : Url -> Model -> ( Model, Cmd Msg )
routeTo newUrl model =
    let
        newRoute =
            Routing.parseUrl newUrl

        newModel =
            { model | route = newRoute }
    in
    case newRoute of
        FrontRoute ->
            ( { newModel | page = FrontPage }, getTimeZone GetTimeZoneReply )

        ClientInfoRoute ->
            ( { newModel | page = ClientInfoPage Nothing }, Ports.getClientInfo () )

        NotFoundRoute ->
            ( { newModel | page = FrontPage }
            , Cmd.batch
                [ Ports.log "log test"
                , Ports.logDebug "log debug"
                , Ports.logError "log error"
                , Ports.logWarning "log warning"
                ]
            )


subscriptions : Model -> Sub Msg
subscriptions model =
    let
        everyHour =
            always >> Time.every (60 * 60 * 1000)
    in
    Sub.batch
        [ Ports.getClientInfoReply ClientInfoReply
        ]
