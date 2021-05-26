const request = require("request");
const fs = require("fs");

process.env["NODE_TLS_REJECT_UNAUTHORIZED"] = "0"; // ignore self-signed SSL errors for localhost
var download = request(
  "https://localhost:5001/types/elm",
  function (error, response, dtos) {
    var file = fs.createWriteStream("src/Dtos.elm");
    file.write(dtos);
  }
);
