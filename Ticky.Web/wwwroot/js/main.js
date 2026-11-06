const OPEN_OFFSET = 8;
// Below Tailwind's `sm` breakpoint, dropdowns marked data-sheet open as a sheet along the bottom of the screen.
const SHEET_MAX_SCREEN_WIDTH = 640;
// Space a sheet leaves above itself, so the page behind stays in sight.
const SHEET_TOP_GAP = 48;

function positionDropdown(dropdownElement, left, top, width = null) {
    const cssPositioned = dropdownElement.dataset.cssPositioned === 'true';
    const sheet = dropdownElement.dataset.sheet === 'true' && document.documentElement.clientWidth < SHEET_MAX_SCREEN_WIDTH;

    dropdownElement.classList.toggle('dropdown-sheet', sheet);
    dropdownElement.classList.remove('hidden');
    dropdownElement.style.visibility = 'hidden';
    if (width !== null) {
        dropdownElement.style.width = width + 'px';
    }

    if (sheet) {
        dropdownElement.style.left = '';
        dropdownElement.style.maxWidth = '';
    } else if (!cssPositioned) {
        // Measure it at the top left first. Placed near the right edge, it would be squeezed into the space left there
        // and keep that narrow width after being moved back on screen.
        dropdownElement.style.left = '0px';
        dropdownElement.style.top = '0px';
        dropdownElement.style.maxWidth = '';
        dropdownElement.style.maxHeight = '';
    }

    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            // Closed again before these frames ran (e.g. a quick tap outside): leave it closed.
            if (openDropdown !== dropdownElement)
                return;

            if (sheet) {
                placeSheet(dropdownElement);
                followViewportWithSheet(dropdownElement);
            } else if (!cssPositioned) {
                let x = left;
                let y = top;

                if (document.documentElement.clientWidth > 0 && document.documentElement.clientHeight > 0) {
                    // Use layout size, not getBoundingClientRect, so the entry animation's translate doesn't skew the math.
                    const width = dropdownElement.offsetWidth;
                    const height = dropdownElement.offsetHeight;
                    const availableWidth = document.documentElement.clientWidth - OPEN_OFFSET;
                    const availableHeight = document.documentElement.clientHeight - OPEN_OFFSET;

                    if (x + width > availableWidth) {
                        if (availableWidth - OPEN_OFFSET > width) {
                            x = availableWidth - width;
                        } else {
                            x = OPEN_OFFSET;
                            dropdownElement.style.maxWidth = (availableWidth - OPEN_OFFSET) + 'px';
                        }
                    }
                    if (y + height > availableHeight) {
                        if (availableHeight - OPEN_OFFSET > height) {
                            y = availableHeight - height;
                        } else {
                            y = OPEN_OFFSET;
                            dropdownElement.style.maxHeight = (availableHeight - OPEN_OFFSET) + 'px';
                        }
                    }
                }

                dropdownElement.style.left = x + 'px';
                dropdownElement.style.top = y + 'px';
            } else if (cssPositioned && document.documentElement.clientHeight > 0) {
                // CSS decides where it opens, so only cap the height to keep the bottom on screen; the box scrolls.
                dropdownElement.style.maxHeight = '';
                const translateY = new DOMMatrixReadOnly(getComputedStyle(dropdownElement).transform).m42;
                const top = dropdownElement.getBoundingClientRect().top - translateY;
                const availableHeight = document.documentElement.clientHeight - top - OPEN_OFFSET;
                if (dropdownElement.offsetHeight > availableHeight) {
                    dropdownElement.style.maxHeight = Math.max(availableHeight, 160) + 'px';
                }
            }
            dropdownElement.style.visibility = 'visible';
            void dropdownElement.offsetWidth;
            dropdownElement.classList.add('dropdown-animate-in');
        });
    });
}

// Puts the sheet's bottom edge on the bottom of the visible screen, so it sits just above the on-screen keyboard.
function placeSheet(sheetElement) {
    const viewport = window.visualViewport;
    const viewTop = viewport ? viewport.offsetTop : 0;
    const viewHeight = viewport ? viewport.height : document.documentElement.clientHeight;

    // Never taller than the visible area, or its top (with the close button) would end up off screen.
    sheetElement.style.maxHeight = Math.min(viewHeight, Math.max(viewHeight - SHEET_TOP_GAP, 160)) + 'px';
    sheetElement.style.top = (viewTop + viewHeight - sheetElement.offsetHeight) + 'px';
}

let stopFollowingViewport = null;

// While a sheet is open, keep it in place when the keyboard opens or closes and when its content changes size.
function followViewportWithSheet(sheetElement) {
    stopFollowingViewportWithSheet();

    const place = () => placeSheet(sheetElement);
    const resizeObserver = new ResizeObserver(place);
    resizeObserver.observe(sheetElement, { box: 'border-box' });
    window.visualViewport?.addEventListener('resize', place);
    window.visualViewport?.addEventListener('scroll', place);

    stopFollowingViewport = () => {
        resizeObserver.disconnect();
        window.visualViewport?.removeEventListener('resize', place);
        window.visualViewport?.removeEventListener('scroll', place);
    };
}

function stopFollowingViewportWithSheet() {
    if (stopFollowingViewport) {
        stopFollowingViewport();
        stopFollowingViewport = null;
    }
}

let openDropdown = null
let lastClosedX = null
let lastClosedY = null
let lastClickTarget = null
let lastOpenedFromTrigger = null
let isPreviousDropdownClosingAnimComplete = true

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

        //console.log('event mouseUp elsewhere')
        lastClosedX = event.clientX
        lastClosedY = event.clientY

        if (lastOpenedFromTrigger === event.target) {
            lastClickTarget = event.target
            //console.log('settign lastClickTarget')
        }

        closeDropdowns()

        setTimeout(resetLastClosed, 500)
    }
})

function resetLastClosed() {
    lastClosedX = null
    lastClosedY = null
    lastClickTarget = null
    //console.log('reset lastClosed')
}

function closeDropdowns() {
    if (!openDropdown)
        return

    //console.log('closing dropdown')

    let targetDropdown = openDropdown
    stopFollowingViewportWithSheet()
    isPreviousDropdownClosingAnimComplete = false
    targetDropdown.classList.remove('dropdown-animate-in')

    setTimeout(() => {
        targetDropdown.classList.add('hidden');
        isPreviousDropdownClosingAnimComplete = true
        //console.log('hidden')
    }, 100);

    openDropdown = null
    lastOpenedFromTrigger = null
}

function openDropDownOnElementId(dropdownElement, triggerElementId) {
    // The same id can exist twice (desktop + mobile layouts); anchor to the one that is actually visible.
    const candidates = Array.from(document.querySelectorAll('[id="' + CSS.escape(triggerElementId) + '"]'));
    const trigger = candidates.find(el => el.getClientRects().length > 0) || candidates[0] || null;
    return openDropDownOnElementPosition(dropdownElement, trigger);
}

function openDropDownOnElementPosition(dropdownElement, triggerElement, matchTriggerWidth = false) {
    if (!dropdownElement || !triggerElement)
        return;

    //console.log('dropdownElement')
    //console.log(dropdownElement)

    //console.log('triggerElement')
    //console.log(triggerElement)

    if (triggerElement === lastClickTarget) {
        closeDropdowns();
        resetLastClosed();
        //console.log('matches lastClickTarget')
        return;
    }

    lastOpenedFromTrigger = triggerElement;
    //console.log('lastOpened ')
    //console.log(lastOpenedFromTrigger)

    setTimeout(() => {
        const clientRect = triggerElement.getBoundingClientRect();
        if (matchTriggerWidth) {
            dropdownElement.style.width = clientRect.width + 'px';
            dropdownElement.style.minWidth = '0';
        }
        positionDropdown(dropdownElement, clientRect.left, clientRect.top + clientRect.height);
        openDropdown = dropdownElement;
        isPreviousDropdownClosingAnimComplete = true;
    }, isPreviousDropdownClosingAnimComplete ? 0 : 100)
}

function onDropdownTriggerClicked(clientX, clientY, dropdownElement) {
    if (!dropdownElement)
        return

    if (lastClosedX == clientX && lastClosedY == clientY) {
        closeDropdowns()
        resetLastClosed()
        //console.log('dropdwonTriggerclicked reset')
        return
    }

    positionDropdown(dropdownElement, clientX + OPEN_OFFSET, clientY + OPEN_OFFSET);
    openDropdown = dropdownElement;
}

async function copyText(text) {
    
    if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
    }

    
    try {
        const textArea = document.createElement("textarea");
        textArea.value = text;

        textArea.style.position = "absolute";
        textArea.style.left = "-9999px";
        textArea.style.top = "0";

        document.body.appendChild(textArea);

        textArea.focus();
        textArea.select();

        const success = document.execCommand('copy');

        document.body.removeChild(textArea);

        if (!success) {
            console.error('Copy command failed for both methods.');
            return false;
        }
    } catch (err) {
        console.error('Fallback copy operation failed:', err);
        return false;
    }
}


let pasteHandler = null;
let pasteDotNetRef = null;

function attachCardPasteHandler(dotNetReference, targetCardId) {
    detachCardPasteHandler();
    pasteDotNetRef = dotNetReference;

    pasteHandler = function (e) {
        const clipboardItems = e.clipboardData && e.clipboardData.items ? e.clipboardData.items : null;

        if (!clipboardItems) 
            return;

        for (let i = 0; i < clipboardItems.length; i++) {
            const item = clipboardItems[i];

            const file = typeof item.getAsFile === 'function' ? item.getAsFile() : null;
            if (!file) continue;
            const formData = new FormData();
            formData.append('file', file, file.name);
            formData.append('cardId', targetCardId);

            fetch('/api/attachments/upload', {
                method: 'POST',
                body: formData,
                credentials: 'same-origin'
            }).then(response => {
                if (!response.ok) {
                    throw new Error(response.status);
                }
                if (pasteDotNetRef) pasteDotNetRef.invokeMethodAsync('OnPastedUploadFinished');
            }).catch(() => {
                if (pasteDotNetRef) pasteDotNetRef.invokeMethodAsync('OnPastedUploadFailed');
            });
        }
    };

    window.addEventListener('paste', pasteHandler);
}

function detachCardPasteHandler() {
    if (pasteHandler) window.removeEventListener('paste', pasteHandler);
    pasteHandler = null;
    pasteDotNetRef = null;
}

function getElementScrollTop(element) {
    try {
        if (!element) return null;
        return element.scrollTop;
    } catch (e) {
        return null;
    }
}

function setElementScrollTop(element, value) {
    try {
        if (!element) return;
        element.scrollTop = value;
    } catch (e) {
    }
}