const OPEN_OFFSET = 5;

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

    const availableWidth = document.documentElement.clientWidth - (OPEN_OFFSET * 2)
    const availableHeight = document.documentElement.clientHeight - (OPEN_OFFSET * 2)

    if (mostRightPoint > availableWidth) {
        if(availableWidth > rect.width) {
            dropdownElement.style.left = availableWidth - rect.width + 'px';
        } else {
            dropdownElement.style.left = OPEN_OFFSET + 'px';
            dropdownElement.style.maxWidth = availableWidth + 'px';
        }
    }
    if (mostBottomPoint > availableHeight) {
        if(availableHeight > rect.height) {
            dropdownElement.style.top = availableHeight - rect.height + 'px';
        } else {
            dropdownElement.style.top = OPEN_OFFSET + 'px';
            dropdownElement.style.maxHeight = availableHeight + 'px';
        }
    }

    void dropdownElement.offsetWidth;
    dropdownElement.classList.add('dropdown-animate-in');
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

        if (lastOpenedFromTrigger === event.target)
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
        element.classList.remove('dropdown-animate-in');
        setTimeout(() => {
            element.classList.add('hidden');
        }, 100);
    });
    openDropdown = null
    lastOpenedFromTrigger = null
}

function openDropDownOnElementId(dropdownElement, triggerElementId) {
    return openDropDownOnElementPosition(dropdownElement, document.getElementById(triggerElementId));
}

function openDropDownOnElementPosition(dropdownElement, triggerElement) {
    if (!dropdownElement || !triggerElement)
        return;

    if (triggerElement === lastClickTarget) {
        closeDropdowns();
        resetLastClosed();
        return;
    }

    lastOpenedFromTrigger = triggerElement;

    const clientRect = triggerElement.getBoundingClientRect();
    positionDropdown(dropdownElement, clientRect.left, clientRect.top + clientRect.height);
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

    positionDropdown(dropdownElement, clientX + OPEN_OFFSET, clientY + OPEN_OFFSET);
    openDropdown = dropdownElement;
}