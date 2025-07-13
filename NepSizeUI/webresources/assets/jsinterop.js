/**
 * Class to handle communication from the WinForms app.
 */
class Interop {
    #registeredCommands = {};

    sendToHostApp(command, payload) {
        if (payload === undefined) {
            window.chrome.webview.postMessage({ "command": command });
        } else {
            window.chrome.webview.postMessage({ "command": command, "payload": payload });
        }
    }

    registerCommand(command, handler) {
        this.#registeredCommands[command] = handler;
    }

    #msgReceived(event) {
        let json = event.data;        

        if (json === null) {
            return;
        }

        if (!('command' in json)) {
            return;
        }

        const command = json.command;
        let payload = {};
        if ('payload' in json) {
            payload = json.payload;
        }

        if (!(command in this.#registeredCommands)) {
            alert("Command not registered");
            return;
        }

        const handler = this.#registeredCommands[command];
        handler(payload);
    }
    
    constructor() {
        window.chrome.webview.addEventListener('message', event => {
            this.#msgReceived(event);            
        });
    }
}

window.interop = new Interop();

/**
 * Inform the WinForms that we're ready.
 */
window.addEventListener("DOMContentLoaded", function () {
    window.interop.sendToHostApp("setReady");
});