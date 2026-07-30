import { register } from "./background/register";
import { registerSocket } from "./background/registerSocket";

self.addEventListener("error", (event) => {
    event.preventDefault();
});

self.addEventListener("unhandledrejection", (event) => {
    event.preventDefault();
});

const socket = registerSocket();
register(socket);
