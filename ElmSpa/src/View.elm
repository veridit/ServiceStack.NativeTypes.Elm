module View exposing (view)

import Browser exposing (Document)
import Html exposing (Html, a, button, div, h1, h2, input, li, p, text, ul)
import Html.Attributes exposing (attribute, class, disabled, href)
import Html.Events exposing (onClick)
import Model exposing (Model, Page(..))
import Update exposing (Msg(..))


title : String
title =
    "Elm Single Place Application"


view : Model -> Document Msg
view model =
    { title = title
    , body = body model
    }


body : Model -> List (Html Msg)
body model =
    case model.page of
        FrontPage ->
            [ h1 [ class "flex" ] [ text title ]
            , p [ class "flex" ] [ text "Welcome" ]
            , p [ class "flex" ]
                [ a [ href "/client_info" ] [ text "Client information" ]
                ]
            ]

        ClientInfoPage maybeClientInfo ->
            [ h1 [ class "flex" ] [ text title ]
            , h2 [] [ text "Client Information for debugging purposes" ]
            , case maybeClientInfo of
                Nothing ->
                    p [] [ text "Fetching..." ]

                Just info ->
                    ul []
                        [ li [] [ text <| "OS: " ++ info.operatingSystem ]
                        , li [] [ text <| "User agent: " ++ info.userAgent ]
                        , li [] [ text <| "Width: " ++ String.fromInt info.width ]
                        , li [] [ text <| "Height: " ++ String.fromInt info.height ]
                        , li [] [ text <| "Language: " ++ info.language ]
                        ]
            , p [ class "flex" ]
                [ a [ href "/" ] [ text "Frontpage" ]
                ]
            ]



{-
   <h2><a href="/json/metadata?op=Hello">Hello</a> API</h2>
   <input type="text" id="txtName" onkeyup="callHello(this.value)">
   <div id="result"></div>

   <div style="font-size:20px;line-height:26px">
       <h3>Using JsonServiceClient in Web Pages</h3>

       <p>
           Update your App's
           <a href="https://docs.servicestack.net/typescript-add-servicestack-reference">TypeScript DTOs</a> and
           compile to JS (requires <a href="https://www.typescriptlang.org/download">TypeScript</a>):
       </p>

       <pre>$ x scripts dtos</pre>

       <h3>Including @servicestack/client &amp; Typed DTOs</h3>

       <p>
           Create a basic UMD loader then include the UMD <b>@servicestack/client</b> library and <b>dtos.js</b>:
       </p>

       <pre>&lt;script&gt;
     var exports = { __esModule:true }, module = { exports:exports }
     function require(name) { return exports[name] || window[name] }
   &lt;/script&gt;
   &lt;script src="/js/servicestack-client.js"&gt;&lt;/script&gt;
   &lt;script src="/dtos.js"&gt;&lt;/script&gt;</pre>

       <p>
           We can then import the library and DTO types in the global namespace to use them directly:
       </p>

       <pre>Object.assign(window, exports) //import

   var client = new JsonServiceClient()
   client.get(new Hello({ name: name }))
       .then(function(r) {
           console.log(r.result)
       })
   </pre>

       <h3>Using @servicestack/client in npm projects</h3>
       <pre>$ npm install @servicestack/client</pre>
       <pre>import { JsonServiceClient } from '@servicestack/client'

   let client = new JsonServiceClient()
   let response = await client.get(new Hello({ name }))
   </pre>

       <p>
           Typed DTOs generated using
           <a href="https://docs.servicestack.net/typescript-add-servicestack-reference">TypeScript Add ServiceStack Reference</a>
       </p>
   </div>
-}
