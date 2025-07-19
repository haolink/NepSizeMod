/**
 * Class to handle communication from the Mod app.
 */
class Interop {
    #registeredEvents = {};
    #ws = null;
    #connected = false;

    sendCommand(command, payload, callback) {
        let msg = {
            "command": command
        };

        if (payload !== undefined) {
            msg["data"] = payload;
        }

		const uuid = Math.random().toString(36).substr(2, 7);
        msg["UUID"] = uuid;
		
		const handler = (e) => {
			let jsonStr = event.data;

			if (jsonStr === null) {
				return;
			}

			let json = null;
			try {
				json = JSON.parse(jsonStr);
			} catch (e) {
				json = null;
			}

			if (json === null) {
				return;
			}
			
			if (!('UUID' in json)) {
				// Only reply to UUID is handled
				return;
			}
			
			if (json.UUID != uuid) {
				return;
			}
						
			this.#ws.removeEventListener('message', handler);
			callback(json);
		};
		
		this.#ws.addEventListener('message', handler);
		const msgJson = JSON.stringify(msg);
		this.#ws.send(msgJson);
    }

    registerEvent(context, handler) {
        this.#registeredEvents[context] = handler;
    }
	
	#fireEventHandler(context, payload) {
		if (!(context in this.#registeredEvents)) {
            return;
        }

        const handler = this.#registeredEvents[context];
        handler(payload);
	}

    #msgReceived(event) {
        let jsonStr = event.data;

        if (jsonStr === null) {
            return;
        }

        let json = null;
        try {
            json = JSON.parse(jsonStr);
        } catch (e) {
            json = null;
        }

        if (json === null) {
            return;
        }

        if (!('Context' in json) || !('Type' in json)) {
            // Only push is handled
            return;
        }

        if (json.Type != 3) {
            // Only push!
            return;
        }

        const context = json.Context;
        let payload = {};
        if ('Data' in json) {
            payload = json.Data;
        }

        this.#fireEventHandler(context, payload);
    }

    constructor() {
        
    }
	
	start() {
		this.#ws = new ReconnectingWebSocket('/socket');
        this.#ws.addEventListener('open', (e) => {
            console.log('Socket connected');
			this.#connected = true;
			this.#fireEventHandler("GameConnected", {});			
        });
		this.#ws.addEventListener('close', (e) => {
			this.#connected = false;
			this.#fireEventHandler("GameDisconnected", {});			
		});
			
        this.#ws.addEventListener('message', (e) => {
            this.#msgReceived(e);
        });
	}
}

window.interop = new Interop();