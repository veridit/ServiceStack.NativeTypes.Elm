{- Options:
     Date: 2021-05-26 09:20:45
     Version: 5.80
     Tip: To override a DTO option, remove "--" prefix before updating
     BaseUrl: https://localhost:5001

   --Package: 
   --GlobalNamespace: Dtos
   --AddPropertyAccessors: True
   --SettersReturnThis: True
   --AddServiceStackTypes: True
   --AddResponseStatus: False
   --AddDescriptionAsComments: True
   --AddImplicitVersion: 
   --IncludeTypes: 
   --ExcludeTypes: 
   --TreatTypesAsStrings: 
   --DefaultImports: import Array exposing (Array),import Dict exposing (Dict),import Http,import Iso8601,import Json.Decode as JD,import Json.Decode.Extra as JDE,import Json.Decode.Pipeline as JDP,import Json.Encode as JE,import Json.Encode.Extra as JEE,import Regex exposing (Regex),import Set exposing (Set),import Time exposing (Posix),import Url,import Url.Parser
-}


module Dtos exposing (..)

import Array exposing (Array)
import Dict exposing (Dict)
import Http
import Iso8601
import Json.Decode as JD
import Json.Decode.Extra as JDE
import Json.Decode.Pipeline as JDP
import Json.Encode as JE
import Json.Encode.Extra as JEE
import Regex exposing (Regex)
import Set exposing (Set)
import Time exposing (Posix)
import Url
import Url.Parser


type Guid
    = Guid String


guidStr : Guid -> String
guidStr (Guid guid) =
    guid


decodeGuid : JD.Decoder Guid
decodeGuid =
    JD.string
        |> JD.map Guid


encodeGuid : Guid -> JE.Value
encodeGuid (Guid guid) =
    JE.string guid


urlParseGuid : Url.Parser.Parser (Guid -> a) a
urlParseGuid =
    Url.Parser.custom "GUID" (Just << (\guid -> Guid guid))


safeRegex : String -> Regex
safeRegex =
    Regex.fromString >> Maybe.withDefault Regex.never


encodeParams : List ( String, String ) -> String
encodeParams params =
    let
        -- Remove any outermost double quotes from json to match the JSV expected by ServiceStack.
        jsvEncode str =
            let
                json2jsv jsonStr =
                    let
                        matcher match =
                            case match.submatches of
                                [ Maybe.Just subStr ] ->
                                    subStr

                                _ ->
                                    match.match
                    in
                    Regex.replace (safeRegex "^\"(.*)\"$") matcher jsonStr

                stripNull jsonStr =
                    let
                        matcher _ =
                            ""
                    in
                    Regex.replace (safeRegex "^null$") matcher jsonStr
            in
            Url.percentEncode <| json2jsv <| stripNull str
    in
    params
        |> List.map (\( key, val ) -> Url.percentEncode key ++ "=" ++ jsvEncode val)
        |> String.join "&"


type alias RequestDefaults requestDto responseDto msg =
    { method : String
    , headers : List Http.Header
    , path : String
    , urlBuilder : String -> requestDto -> String
    , bodyBuilder : requestDto -> Http.Body
    , expectBuilder : (Result Http.Error responseDto -> msg) -> Http.Expect msg
    , timeout : Maybe Float
    , tracker : Maybe String
    }


type alias Request msg =
    { method : String
    , headers : List Http.Header
    , url : String
    , body : Http.Body
    , expect : Http.Expect msg
    , timeout : Maybe Float
    , tracker : Maybe String
    }


requestFromDefaults : RequestDefaults requestDto responseDto msg -> requestDto -> (Result Http.Error responseDto -> msg) -> Request msg
requestFromDefaults defaults requestDto msgBuilder =
    { url = defaults.urlBuilder defaults.path requestDto
    , method = defaults.method
    , body = defaults.bodyBuilder requestDto
    , expect = defaults.expectBuilder msgBuilder
    , timeout = defaults.timeout
    , tracker = defaults.tracker
    , headers = defaults.headers
    }


type alias Hello =
    { name : String
    }


decodeHello : JD.Decoder Hello
decodeHello =
    JD.succeed Hello
        |> JDP.required "name" JD.string


encodeHello : Hello -> JE.Value
encodeHello record =
    let
        encodedname =
            JE.string record.name
    in
    JE.object
        [ ( "name", encodedname )
        ]


sendHello : Hello -> (Result Http.Error HelloResponse -> msg) -> Cmd msg
sendHello requestDto msgBuilder =
    Http.request <| requestFromDefaults sendHelloDefaults requestDto msgBuilder


sendHelloDefaults : RequestDefaults Hello HelloResponse msg
sendHelloDefaults =
    { path = "/hello"
    , urlBuilder =
        \path requestDto ->
            let
                params =
                    [ ( "name", JE.encode 0 <| JE.string requestDto.name )
                    ]
            in
            path ++ "?" ++ encodeParams params
    , method = "GET"
    , bodyBuilder = \_ -> Http.emptyBody
    , expectBuilder = \msgBuilder -> Http.expectJson msgBuilder decodeHelloResponse
    , timeout = Just {- milliseconds -} 30000
    , tracker = Nothing
    , headers = [ Http.header "Accept" "application/json" ]
    }


type alias HelloResponse =
    { result : String
    }


decodeHelloResponse : JD.Decoder HelloResponse
decodeHelloResponse =
    JD.succeed HelloResponse
        |> JDP.required "result" JD.string


encodeHelloResponse : HelloResponse -> JE.Value
encodeHelloResponse record =
    let
        encodedresult =
            JE.string record.result
    in
    JE.object
        [ ( "result", encodedresult )
        ]
