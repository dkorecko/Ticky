let openDropdown = null
let lastClosedX = null
let lastClosedY = null
let lastClickTarget = null
let lastOpenedFromTrigger = null

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

    const rect = dropdownElement.getBoundingClientRect()
    const mostRightPoint = rect.width + clientX + 5
    const mostBottomPoint = rect.height + clientY + 5

    console.log(rect)
    console.log(window.screen)

    if (mostRightPoint > window.screen.width) {
        console.log('left offset')
        console.log(window.screen.width - rect.width + 'px')
        dropdownElement.style.left = window.screen.width - rect.width + 'px'
    }

    if (mostBottomPoint > window.screen.height) {
        console.log('top offset')
        console.log(window.screen.height - rect.height + 'px')
        dropdownElement.style.top = window.screen.height - rect.height + 'px'
    }

    openDropdown = dropdownElement
}