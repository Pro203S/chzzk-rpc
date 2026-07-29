import { register } from "./background/register";
import { registerSocket } from "./background/registerSocket";

console.log("[Discheese] background service worker 시작");
registerSocket();
register();
