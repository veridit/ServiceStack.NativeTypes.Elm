module Update exposing (Msg(..), init, subscriptions, update)

import Browser exposing (UrlRequest(..))
import Browser.Navigation as Navigation
import Dtos
import Http
import Model exposing (Flags, Model)
import Ports
import Result exposing (Result)
import Routing exposing (Route(..))
import Url exposing (Url)


type Msg
    = OnUrlChange Url
    | OnUrlRequest UrlRequest
    | Hello String
    | HelloResponse (Result Http.Error Dtos.HelloResponse)
    | TestLogging


init : Flags -> Url -> Navigation.Key -> ( Model, Cmd Msg )
init _ url navKey =
    ( { navKey = navKey
      , route = Routing.parseUrl url
      , name = ""
      , helloResponse = Nothing
      }
    , Cmd.none
    )


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        OnUrlRequest urlRequest ->
            case urlRequest of
                Internal newUrl ->
                    ( model, Navigation.pushUrl model.navKey <| Url.toString newUrl )

                External newUrl ->
                    ( model, Navigation.load newUrl )

        OnUrlChange newUrl ->
            ( { model | route = Routing.parseUrl newUrl }, Cmd.none )

        Hello changedName ->
            ( model
            , Dtos.sendHello { name = changedName } HelloResponse
            )

        HelloResponse res ->
            case res of
                Ok helloResponse ->
                    ( { model | helloResponse = Just helloResponse }, Cmd.none )

                Err _ ->
                    ( model, Ports.logError "There was a server problem" )

        TestLogging ->
            ( model
            , Cmd.batch
                [ Ports.log "log test"
                , Ports.logDebug "log debug"
                , Ports.logError "log error"
                , Ports.logWarning "log warning"
                ]
            )


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none
