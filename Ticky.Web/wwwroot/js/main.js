function positionDropdown(dropdownElement, left, top, width = null) {
    dropdownElement.classList.remove('hidden');
    if (width !== null) {
        dropdownElement.style.width = width + 'px';
    }
    dropdownElement.style.left = left + 'px';
    dropdownElement.style.top = top + 'px';

    const rect = dropdownElement.getBoundingClientRect();
    const mostRightPoint = left + rect.width;
    const mostBottomPoint = top + rect.height;

    if (mostRightPoint > window.innerWidth) {
        dropdownElement.style.left = window.innerWidth - rect.width + 'px';
    }
    if (mostBottomPoint > window.innerHeight) {
        dropdownElement.style.top = window.innerHeight - rect.height + 'px';
    }
}
let openDropdown = null
let lastClosedX = null
let lastClosedY = null
let lastClickTarget = null
let lastOpenedFromTrigger = null

function triggerConfetti(x, y) {
    if (typeof confetti === 'undefined') {
        console.warn('Confetti library not loaded. Skipping confetti trigger.')
        return
    }
    confetti({
        particleCount: 200,
        angle: 20 + (Math.random()*170),
        spread: 70,
        origin: { y: y, x: x },
        disableForReducedMotion: true
    });
}

document.addEventListener('mouseup', (event) => {
    if (openDropdown) {
        if (openDropdown.contains(event.target))
            return

        lastClosedX = event.clientX
        lastClosedY = event.clientY

        if (lastOpenedFromTrigger == event.target)
            lastClickTarget = event.target

        closeDropdowns()

        setTimeout(resetLastClosed, 500)
    }
})

function resetLastClosed() {
    lastClosedX = null
    lastClosedY = null
    lastClickTarget = null
}

function closeDropdowns() {
    document.querySelectorAll('.dropdown').forEach(element => {
        element.classList.add('hidden')
    })
    openDropdown = null
    lastOpenedFromTrigger = null
}

function openDropDownOnElementId(dropdownElement, triggerElementId) {
    return openDropDownOnElementPosition(dropdownElement, document.querySelector("#" + triggerElementId));
}

function openDropDownOnElementPosition(dropdownElement, triggerElement) {
    if (!dropdownElement || !triggerElement)
        return;

    if (triggerElement == lastClickTarget) {
        closeDropdowns();
        resetLastClosed();
        return;
    }

    lastOpenedFromTrigger = triggerElement;

    var clientRect = triggerElement.getBoundingClientRect();
    var left = clientRect.left;
    var top = clientRect.top + clientRect.height;
    positionDropdown(dropdownElement, left, top);
    openDropdown = dropdownElement;
}

function onDropdownTriggerClicked(clientX, clientY, dropdownElement) {
    if (!dropdownElement)
        return

    if (lastClosedX == clientX && lastClosedY == clientY) {
        closeDropdowns()
        resetLastClosed()
        return
    }

    positionDropdown(dropdownElement, clientX + 5, clientY + 5);
    openDropdown = dropdownElement;
}