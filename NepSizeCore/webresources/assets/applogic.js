/**
  * Conversion of string to HTML entities
  */
String.prototype.toHtmlEntities = function () {
    return this.replace(/./gm, function (s) {
        // return "&#" + s.charCodeAt(0) + ";";
        return (s.match(/[a-z0-9\s]+/i)) ? s : '&#' + s.charCodeAt(0) + ';';
    });
};

let debugMode = false; //Is debug mode on
let activeCharacterIds = []; //Who are the active characters

/**
 * Updates the character highlights (blue) based on the list of active characters.
 */
function updateHighlitCharacters() {
    document.querySelectorAll("tr[data-char-id].c-active").forEach(input => {
        input.classList.remove("c-active");
    });
    document.querySelectorAll("option.c-active").forEach(input => {
        input.classList.remove("c-active");
    });

    activeCharacterIds.forEach(function (id) {
        document.querySelector(`option[value="${id}"]`)?.classList.add("c-active");
        document.querySelector(`tr[data-char-id="${id}"]`)?.classList.add("c-active");
    });
}

/**
 * We received a new player list.
 */
window.interop.registerCommand("UpdatePlayerList", function (data) {
    activeCharacterIds = data.characterIds;

    displayDebugActiveCharacters();

    updateHighlitCharacters();
});


let lastGame = null; //Game ID

/**
 * Displaying some debug data.
 */
function displayDebugActiveCharacters() {
    let allIds = null;
    if (lastGame in gameCharacters) {
        allIds = [];
        Object.entries(gameCharacters[lastGame]).forEach(([group, characters]) => {
            Object.entries(characters).forEach(([i, characterData]) => {
                allIds[characterData.id] = characterData.name;
            });
        });        
    }

    let actList = [];
    activeCharacterIds.forEach((id) => {
        if (allIds != null) {
            if (!(id in allIds)) {
                actList.push(`<span class="debug-unknown">${id}</span>`);
            } else {
                actList.push(`${allIds[id]} (${id})`);
            }
        } else {
            actList.push(`${id}`);
        }
    });
    document.querySelector('#char-ids').innerHTML = actList.join(', ');
}

/**
 * Game has connected.
 */
window.interop.registerCommand("GameConnected", function (data) {
    document.querySelector("#character-id").removeAttribute("disabled");
    document.querySelector("#btn-add-all").removeAttribute("disabled");
    document.querySelector("#btn-add-character").removeAttribute("disabled");
    document.querySelector("#btn-persist").removeAttribute("disabled");
    document.querySelector("#btn-unpersist").removeAttribute("disabled");

    const newGame = data.game; //Is it another one? Reset the UI and repopulate the selections.
    if (newGame != lastGame) {
        clearChars();
        lastGame = newGame;
        if (newGame in gameCharacters) {
            populateMenu(gameCharacters[newGame]);

            Object.entries(data.currentScales).forEach(([id, scale]) => {
                addCharacterScale(id, scale);
            });
            updateHighlitCharacters();
            updateScales();
        }        
    } else { //Otherwise enable the fields.
        document.querySelectorAll('input[type=text][data-char-id]').forEach((e) => {
            e.removeAttribute("disabled");
        });
        document.querySelectorAll('button.btn-remove-char').forEach((e) => {
            e.removeAttribute("disabled");
        });
        updateScales(); //And send the scales
    }
});

/**
 * Game disconnected - disable the inputs.
 */
window.interop.registerCommand("GameDisconnected", function (data) {
    document.querySelector("#character-id").setAttribute("disabled", "disabled");
    document.querySelector("#btn-add-all").setAttribute("disabled", "disabled");
    document.querySelector("#btn-add-character").setAttribute("disabled", "disabled");
    document.querySelector("#btn-persist").setAttribute("disabled", "disabled");
    document.querySelector("#btn-unpersist").setAttribute("disabled", "disabled");

    document.querySelectorAll('input[type=text][data-char-id]').forEach((e) => {
        e.setAttribute("disabled", "disabled");
    });
    document.querySelectorAll('button.btn-remove-char').forEach((e) => {
        e.setAttribute("disabled", "disabled");
    });
});

/**
 * Show another text in the <select> field based on the selection.
 */
function updateSelectList() {
    const select = document.querySelector("#character-id");
    const option = select.selectedOptions[0];

    option.textContent = option.getAttribute("data-name");
}

/**
 * Show the original text for all <option> values.
 */
function resetSelectList() {
    const select = document.querySelector("#character-id");
    select.querySelectorAll('option').forEach(option => {
        option.textContent = option.getAttribute("data-text");
    });
}

/**
 * We're ready, set up the select field.
 */
window.addEventListener("DOMContentLoaded", () => {
    const select = document.querySelector("#character-id");

    ["change", "blur"].forEach(event => {
        select.addEventListener(event, () => {
            updateSelectList();
        });
    });
    select.addEventListener("pointerdown", () => {
        resetSelectList();
    });    
});

/**
 * Fill in the character list.
 * @param {any} characters
 */
function populateMenu(characters) {
    const select = document.querySelector("#character-id");

    let html = "";
    Object.entries(characters).forEach(([group, characters]) => {
        html += "<optgroup label=\"" + group + "\">";

        Object.entries(characters).forEach(([i, characterData]) => {
            html += "<option value=\"" + characterData.id + "\" data-name=\"" + characterData.name.toHtmlEntities() + "\" data-text=\"" + characterData.text.toHtmlEntities() + "\">" + characterData.name.toHtmlEntities() + "</option>";
        });

        html += "</optgroup>";
    });

    select.innerHTML = html;

    updateSelectList();
};

/**
 * Parse numbers based on the user locale.
 * @param {any} stringNumber
 * @returns
 */
function parseLocaleNumber(stringNumber) {
    const example = Intl.NumberFormat(undefined).format(1.1);
    const decimalSeparator = example.replace(/\d/g, '');

    // Baue Regex dynamisch
    const regex = new RegExp(`^-?\\d+(\\${decimalSeparator}(\\d+)?)?$`);

    if (!regex.test(stringNumber.trim())) {
        return NaN;
    }

    // Wandelt Trenner in Punkt, damit JS parsen kann
    const normalized = stringNumber.replace(decimalSeparator, '.');
    return Number(normalized);
}

/**
 * Send scales to the game.
 * @param {any} scales
 */
function sendScales(scales) {
    window.interop.sendToHostApp("updateScales", { "scales": scales });
}

/**
 * Determine scales from the UI.
 */
function updateScales() {
    let scales = {};
    document.querySelectorAll("input[type=text][data-char-id]").forEach(input => {
        const characterId = input.getAttribute("data-char-id");
        const scale = parseLocaleNumber(input.value);
        let invalid = false;
        if (Number.isNaN(scale) || !Number.isFinite(scale) || (scale <= 0)) {
            invalid = true;
        }

        if (invalid) {
            if (!input.classList.contains("v-invalid")) {
                input.classList.add("v-invalid");
            }
        } else {
            if (input.classList.contains("v-invalid")) {
                input.classList.remove("v-invalid");
            }

            scales[characterId] = scale;
        }
    });

    sendScales(scales);
}

/**
 * Add a character to the UI list.
 * @param {any} characterName
 * @param {any} characterId
 * @param {any} scale
 * @returns
 */
function addCharacterByNameAndId(characterName, characterId, scale) {
    const fieldId = `input[type="text"][data-char-id="${characterId}"]`;
    const inputField = document.querySelector(fieldId);
    scale = scale ?? 1.0;

    if (inputField != null) {
        return false;
    }

    const characterList = document.querySelector("#char-list");
    const emptyRow = document.querySelector(`tr#no-characters`);

    const newNode = document.createElement('tr');
    newNode.setAttribute('data-char-id', characterId);

    const initialScale = (scale).toLocaleString(undefined, { minimumFractionDigits: 1 });
    newNode.innerHTML = `
        <td style="text-align: right" scope="row"><label for="char-size-${characterId}" class="col-form-label">${characterName}</label ></td >
        <td><input type="text" class="form-control" id="char-size-700" data-char-id="${characterId}" value="${initialScale}" onchange="updateScales()" onfocus="updateScales()" onkeyup="updateScales()" /></td>
        <td><button type="button" onclick="return removeChar(${characterId})" class="btn btn-danger btn-remove-char">X</button></td>
`;

    characterList.insertBefore(newNode, emptyRow);
}

/**
 * Add all known characters to the UI.
 * @returns
 */
function addAllActive() {
    let activeKnown = [];

    if (lastGame == null || !(lastGame in gameCharacters)) {
        return false;
    }

    Object.entries(gameCharacters[lastGame]).forEach(([group, characters]) => {
        Object.entries(characters).forEach(([i, characterData]) => {
            if (activeCharacterIds.includes(parseInt(characterData.id))) {
                activeKnown.push({
                    id: characterData.id,
                    name: characterData.name
                });
            }
        });
    });

    if (activeKnown.length > 0) {
        activeKnown.forEach(data => {
            addCharacterByNameAndId(data.name, data.id, 1.0);
        });

        toggleEmptyLine();
        updateHighlitCharacters();
        updateScales();
    }

    return false;
}

/**
 * Add a character by their ID to the UI.
 * @param {any} id
 * @param {any} scale
 * @returns
 */
function addCharacterScale(id, scale) {
    const characterOption = document.querySelector(`option[value="${id}"]`);
    if (characterOption == null) {
        return;
    }

    const characterName = characterOption.getAttribute("data-name");

    addCharacterByNameAndId(characterName, id, scale);

    toggleEmptyLine();
}

/**
 * Add the character selected in the <select> field.
 * @returns
 */
function addCharacter() {
    const selectedOption = document.querySelector('#character-id')?.selectedOptions[0];

    if (selectedOption == null) {
        return false;
    }

    const characterName = selectedOption.getAttribute("data-name");
    const characterId = selectedOption.value;

    addCharacterByNameAndId(characterName, characterId, 1.0);

    toggleEmptyLine();
    updateHighlitCharacters();
    updateScales();

    return false;
}

/**
 * Clear the UI.
 */
function clearChars() {
    document.querySelectorAll(`tr[data-char-id]`).forEach((e) => {
        e.remove();
    });
    toggleEmptyLine();
}

/**
 * Remove a character from the UI.
 * @param {any} characterId
 * @returns
 */
function removeChar(characterId) {
    const charRow = document.querySelector(`tr[data-char-id="${characterId}"]`);
    if (charRow == null) {
        return;
    }
    charRow.remove();
    toggleEmptyLine();
    return false;
}

/**
 * If no character is added int he UI, show the empty line.
 */
function toggleEmptyLine() {
    const field = document.querySelector(`input[type="text"][data-char-id]`);
    const emptyRow = document.querySelector(`tr#no-characters`);

    if (field != null) {
        if (!emptyRow.classList.contains('dont-show')) {
            emptyRow.classList.add('dont-show');
        }
    } else {
        if (emptyRow.classList.contains('dont-show')) {
            emptyRow.classList.remove('dont-show');
        }
    }
}

/**
 * Debug on/off.
 */
window.interop.registerCommand("EnableDebug", () => {
    debugMode = true;
    document.querySelectorAll('.debug-hide').forEach((e) => {
        e.classList.remove('debug-hide');
    });
});

/**
 * Persist scales in game.
 * @returns
 */
function persist() {
    window.interop.sendToHostApp("persistScales");
    return false;
}

/**
 * Delete persistence information.
 * @returns
 */
function unpersist() {
    window.interop.sendToHostApp("clearPersistance");
    return false;
}

/**
 * Persistence callback.
 */
window.interop.registerCommand("ConfirmPersistenceUpdate", function (success) {
    alert((success) ? "Saved" : "Something went wrong.");
});