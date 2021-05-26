module View exposing (view)

import Browser exposing (Document)
import Html exposing (Html, a, div, h2, h3, input, p, pre, text)
import Html.Attributes exposing (href, id, style, type_)
import Html.Events exposing (onInput)
import Model exposing (Model)
import Update exposing (Msg(..))


title : String
title =
    "Elm Using ServiceStack"


view : Model -> Document Msg
view model =
    { title = title
    , body = body model
    }


body : Model -> List (Html Msg)
body model =
    [ h2 [] [ a [ href "/json/metadata?op=Hello" ] [ text "Hello" ], text "API" ]
    , input [ type_ "text", onInput Hello ] []
    , div [ id "result" ]
        [ case model.helloResponse of
            Nothing ->
                text ""

            Just { result } ->
                text result
        ]
    , div [ style "font-size" "20px", style "line-height" "26px" ]
        [ h3 [] [ text "Using API generated Elm code" ]
        , p []
            [ text "Add or update your App's Elm DTOs"
            , pre [] [ text "curl -q https://localhost:5001/types/elm > src/Dtos.elm" ]
            , text "then add the required package dependencies"
            , pre []
                [ text "elm install elm-community/json-extra"
                , text "elm install elm/regex"
                , text "elm install rtfeldman/elm-iso8601-date-strings"
                ]
            , text "and compile with"
            , a [ href "https://elm-lang.org" ] [ text "Elm" ]
            ]
        , pre []
            [ text "import Dtos"
            , text ""
            , text "update msg model ="
            , text "    case msg of"
            , text "        SendApiRequest ->"
            , text "            (model, Dtos.sendApiRequest { param1 = 1, param2 = \"two\" } SendApiResponse)"
            , text "        SendApiResponse res ->"
            , text "            case res of"
            , text "                Ok response ->"
            , text "                    ({model | apiResponse = response}, Cmd.none)"
            , text "                Err _ ->"
            , text "                    (model, Port.logError \"Custom error message\")"
            , text "            (model, Dtos.sendApiRequest { param1 = 1, param2 = \"two\" } SendApiResponse)"
            ]
        ]
    ]
