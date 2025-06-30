function setElementHeightBasedOnAvailableSpace(targetElementsClass, excludePaddingAndScrollId, elementsToExcludeIds = []) {
    const targetElements = document.querySelectorAll('.' + targetElementsClass);

    if (targetElements.length === 0) {
        return
    }

    let availableHeight = window.innerHeight;

    elementsToExcludeIds.forEach(id => {
        const excludeElement = document.getElementById(id);
        if (excludeElement) {
            availableHeight -= excludeElement.offsetHeight;
        } else {
            console.warn(`Excluded element with ID '${id}' not found.`);
        }
    });

    const excludePaddingAndScroll = document.getElementById(excludePaddingAndScrollId)

    if (excludePaddingAndScroll) {
        let computedStyles = window.getComputedStyle(excludePaddingAndScroll)

        availableHeight -= parseFloat(computedStyles.paddingTop);
        availableHeight -= parseFloat(computedStyles.paddingBottom);
    }

    const hasHorizontalScrollbar = excludePaddingAndScroll.scrollWidth > excludePaddingAndScroll.clientWidth;

    if (hasHorizontalScrollbar) {
        // The difference between offsetHeight and clientHeight will be the height of the horizontal scrollbar
        // (plus any border-bottom if not accounted for elsewhere).
        // For a simpler case, this often gives the scrollbar height directly.
        availableHeight -= excludePaddingAndScroll.offsetHeight - excludePaddingAndScroll.clientHeight;
    }

    targetElements.forEach(element => {
        // Apply the calculated height to the target element
        element.style.height = `${availableHeight}px`;
    })
}

function scaleRun() {
    setElementHeightBasedOnAvailableSpace('board-column', 'board-section', ['nav-menu', 'board-header'])
}

function scaleInitialize() {
    scaleRun()

    window.addEventListener('resize', () => {
        scaleRun()
    })
}