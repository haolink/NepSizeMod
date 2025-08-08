/**
  * Conversion of string to HTML entities
  */
String.prototype.toHtmlEntities = function () {
    return this.replace(/./gm, function (s) {
        // return "&#" + s.charCodeAt(0) + ";";
        return (s.match(/[a-z0-9\s]+/i)) ? s : '&#' + s.charCodeAt(0) + ';';
    });
};

let activeCharacterIds = []; //Who are the active characters
let listPopulated = false;

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
window.interop.registerEvent("ActiveCharacterChange", function (data) {
    setActiveCharacters(data);
});

/**
 * Handles the list active characters.
 * @param {any} ids
 */
function setActiveCharacters(ids) {
	activeCharacterIds = ids;

    displayDebugActiveCharacters();

    updateHighlitCharacters();
}


/**
 * Displaying some debug data.
 */
function displayDebugActiveCharacters() {
	allIds = [];
	Object.entries(characterData).forEach(([group, characters]) => {
		Object.entries(characters).forEach(([i, characterData]) => {
			allIds[characterData.id] = characterData.name;
		});
	});        

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
window.interop.registerEvent("GameConnected", function () {
    document.querySelector("#character-id").removeAttribute("disabled");
    document.querySelector("#btn-add-all").removeAttribute("disabled");
    document.querySelector("#btn-add-character").removeAttribute("disabled");
    document.querySelector("#btn-persist").removeAttribute("disabled");
    document.querySelector("#btn-unpersist").removeAttribute("disabled");

	if (!listPopulated) {
		clearChars();
		populateMenu(characterData);

		updateHighlitCharacters();
		
		window.interop.sendCommand("GetCurrentScales", {}, (reply) => {
			reply.Data.scales.forEach((entry) => {
				addCharacterScale(entry.id, entry.scale);
			});
			
			updateHighlitCharacters();
			updateScales();
		});
	} else {
		updateScales();
	}

	document.querySelectorAll('input[type=text][data-char-id]').forEach((e) => {
		e.removeAttribute("disabled");
	});
	document.querySelectorAll('button.btn-remove-char').forEach((e) => {
		e.removeAttribute("disabled");
	});    
	
	window.interop.sendCommand("GetActiveCharacterIds", {}, (reply) => {
		setActiveCharacters(reply.Data.ids);
	});
});

/**
 * Game disconnected - disable the inputs.
 */
window.interop.registerEvent("GameDisconnected", function (data) {
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
    if ('siteTitle' in window) {
        document.title = window.siteTitle;
    }

    const select = document.querySelector("#character-id");

    if (window.debugMode) {
        document.querySelectorAll('.debug-hide').forEach((e) => {
            e.classList.remove('debug-hide');
        });
    }

    ["change", "blur"].forEach(event => {
        select.addEventListener(event, () => {
            updateSelectList();
        });
    });
    select.addEventListener("pointerdown", () => {
        resetSelectList();
    });

    // Does the game provide extra settings?
    if ('extraSettings' in window && window.extraSettings !== null) {
        const exSettings = document.querySelector('#extra-settings');
        exSettings.style.display = 'table-row-group';

        let first = true;

        // populate a list of them
        window.extraSettings.forEach((st) => {
            if (st.Type != 'bool' && st.Type != 'float') {
                return;
            }

            const newRow = document.createElement('tr');
            newRow.setAttribute('data-ex-setting-name', st.Name);
            newRow.setAttribute('data-ex-setting-type', st.Type);
            newRow.style.display = 'none';

            if (first) {
                newRow.style.borderTop = 'solid 2px black';                
                first = false;
            }

            let html = ` 
                <td style="text-align: right" scope="row"><label for="ex-setting-${st.Name}" class="col-form-label-sm">${st.Description}</label ></td >
                <td style="vertical-align: middle">`;

            if (st.Type == 'bool') {
                html += `<input type="checkbox" class="form-check-input-sm" name="ex-setting-${st.Name}" id="ex-setting-${st.Name}"` + (st.Value === true ? ' checked="checked"' : '') + ' onchange="updateExSettings()" onfocus="updateExSettings()" onkeyup="updateExSettings()">';
            } else if (st.Type == 'float') {
                html += `<input class="form-control-sm" type="text" name="ex-setting-${st.Name}" id="ex-setting-${st.Name}" value="` + formatLocaleNumber(st.Value) + '" onchange="updateExSettings()" onfocus="updateExSettings()" onkeyup="updateExSettings()">';
            }

            html += `</td>
                <td>&nbsp;</td>
            `

            newRow.innerHTML = html;
            exSettings.appendChild(newRow);
        });
    }

	window.interop.start();
});

/**
 * Display or hide extra settings.
 */
function toggleSettings() {
    const settings = document.querySelectorAll('tr[data-ex-setting-name]');
    if (settings.length == 0) {
        return;
    }

    const currentlyVisible = (settings[0].style.display != 'none');

    settings.forEach((e) => {
        e.style.display = currentlyVisible ? 'none' : 'table-row';
    });

    document.querySelectorAll('.ex-set-open').forEach((e) => {
        e.style.display = currentlyVisible ? 'none' : 'inline';
    });

    document.querySelectorAll('.ex-set-closed').forEach((e) => {
        e.style.display = currentlyVisible ? 'inline' : 'none';
    });
}

/**
 * Method which collects the values of extra settings - if available.
 */
function updateExSettings() {
    const settingsRows = document.querySelectorAll('tr[data-ex-setting-name]');

    const settingsSubmission = {};
    let settingsSet = false;

    settingsRows.forEach((row) => {
        const name = row.getAttribute('data-ex-setting-name');
        const type = row.getAttribute('data-ex-setting-type');

        let value = null;

        if (type == 'bool') {
            value = row.querySelector('input').checked;
        } else if (type == 'float') {
            const inputField = row.querySelector('input');
            const inputValue = parseLocaleNumber(inputField.value);

            let invalid = false;
            if (Number.isNaN(inputValue) || !Number.isFinite(inputValue)) {
                invalid = true;
            }

            if (invalid) {
                if (!inputField.classList.contains("v-invalid")) {
                    inputField.classList.add("v-invalid");
                }
            } else {
                if (inputField.classList.contains("v-invalid")) {
                    inputField.classList.remove("v-invalid");
                }

                value = inputValue;
            }
        }

        if (value != null) {
            settingsSet = true;
            settingsSubmission[name] = value;
        }
    });

    if (settingsSet) {
        window.interop.sendCommand("UpdateExtraSettings", { settings: settingsSubmission }, (e) => { console.log(e); });
    }    
}

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
    listPopulated = true;

    updateSelectList();
};

/**
 * Parse numbers based on the user locale.
 * @param {any} stringNumber
 * @returns
 */
function parseLocaleNumber(stringNumber) {
    const decimalSeparator = window.userDecimalSeparator || '.';

    // Build dynamic RegEx
    const regex = new RegExp(`^-?\\d+(\\${decimalSeparator}(\\d+)?)?$`);

    if (!regex.test(stringNumber.trim())) {
        return NaN;
    }

    // Converts decimal separator to dot
    const normalized = stringNumber.replace(decimalSeparator, '.');
    return Number(normalized);
}

/**
 * Formats a number using the locale decimal separator.
 * @param {any} number
 * @returns
 */
function formatLocaleNumber(num) {
    const decimalSeparator = window.userDecimalSeparator || '.';

    if (!Number.isFinite(num)) {
        return '';
    }

    // Numbers above 1 - display 1 decimal digit unless that one's 0.
    if (Math.abs(num) >= 1) {
        const rounded = Math.round(num * 10) / 10;
        const [intPart, fracPart] = rounded.toString().split('.');

        if (fracPart && fracPart !== '0') {
            return `${intPart}${decimalSeparator}${fracPart}`;
        } else {
            return intPart;
        }
    }

    // Numbers below 0: 2 significant digits.
    const significant = num.toPrecision(2);
    const asFloat = parseFloat(significant);
    const str = asFloat.toString();

    const [intPart, fracPart] = str.split('.');
    if (fracPart) {
        return `${intPart}${decimalSeparator}${fracPart}`;
    } else {
        return intPart;
    }
}

/**
 * Send scales to the game.
 * @param {any} scales
 */
function sendScales(scales) {
	const scaleArray = new Array();
	Object.entries(scales).forEach(([id, scale]) => scaleArray.push({ id: parseInt(id), scale: scale }));
	const payload = { "scales": scaleArray, "overwride": true };
    window.interop.sendCommand("SetScales", payload, (e) => {  });
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

    const initialScale = formatLocaleNumber(scale);
    newNode.innerHTML = `
        <td style="text-align: right" scope="row"><label for="char-size-${characterId}" class="col-form-label-sm">${characterName}</label ></td >
        <td><input type="text" class="form-control-sm" id="char-size-${characterId}" data-char-id="${characterId}" value="${initialScale}" onchange="updateScales()" onfocus="updateScales()" onkeyup="updateScales()" /></td>
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

    Object.entries(characterData).forEach(([group, characters]) => {
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
 * Persist scales in game.
 * @returns
 */
function persist() {
    window.interop.sendCommand("UpdatePersistence", { clear: false }, (e) => { console.log(e); });
    return false;
}

/**
 * Delete persistence information.
 * @returns
 */
function unpersist() {
    window.interop.sendCommand("UpdatePersistence", { clear: true }, (e) => { console.log(e); });
    return false;
}
