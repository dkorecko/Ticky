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

function openDropDownOnElementId(dropdownElement, triggerElementId, adjustSize) {
    return openDropDownOnElementPosition(dropdownElement, document.querySelector("#" + triggerElementId), adjustSize)
}

function openDropDownOnElementPosition(dropdownElement, triggerElement, adjustSize) {
    if (!dropdownElement || !triggerElement)
        return

    if (triggerElement == lastClickTarget) {
        closeDropdowns()
        resetLastClosed()
        return
    }

    lastOpenedFromTrigger = triggerElement

    dropdownElement.classList.remove('hidden')
    var clientRect = triggerElement.getBoundingClientRect();
    dropdownElement.style.top = clientRect.top + clientRect.height + 'px'
    dropdownElement.style.left = clientRect.left + 'px'
    if(adjustSize) {
        dropdownElement.style.width = clientRect.width + 'px'
    }
    openDropdown = dropdownElement
}

function onDropdownTriggerClicked(clientX, clientY, dropdownElement) {
    if (!dropdownElement)
        return

    if (lastClosedX == clientX && lastClosedY == clientY) {
        closeDropdowns()
        resetLastClosed()
        return
    }

    dropdownElement.classList.remove('hidden')
    dropdownElement.style.top = clientY + 5 + 'px'
    dropdownElement.style.left = clientX + 5 + 'px'
    openDropdown = dropdownElement
}