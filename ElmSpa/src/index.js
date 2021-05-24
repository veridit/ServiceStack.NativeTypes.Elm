import { Elm } from "./App.elm";

const app = Elm.App.init({
  flags: {},
});

app.ports.log.subscribe(console.log);

app.ports.logError.subscribe(console.error);

app.ports.logWarning.subscribe(console.warn);

app.ports.logDebug.subscribe(console.debug);

app.ports.getClientInfo.subscribe(function () {
  app.ports.getClientInfoReply.send({
    operatingSystem: navigator.platform,
    userAgent: navigator.userAgent,
    width: window.innerWidth,
    height: window.innerHeight,
    language: navigator.language,
  });
});
