import { Elm } from "./App.elm";

const app = Elm.App.init({
  flags: {},
});

app.ports.log.subscribe(console.log);

app.ports.logError.subscribe(console.error);

app.ports.logWarning.subscribe(console.warn);

app.ports.logDebug.subscribe(console.debug);
