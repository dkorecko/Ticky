function setElementHeightBasedOnAvailableSpace(targetElementId, excludePaddingAndScrollId, elementsToExcludeIds = []) {
    for (let i = 0; i < 1000000; i++) {
        const targetElementResultId = `${targetElementId}-${i}`;
        const targetElement = document.getElementById(targetElementResultId);

        if (!targetElement) {
            console.warn(`Target element with ID '${targetElementResultId}' not found.`);
            return;
        }

        // Get the viewport height
        let availableHeight = window.innerHeight;

        // Subtract the height of elements that should not be covered by the target
        elementsToExcludeIds.forEach(id => {
            const excludeElement = document.getElementById(id);
            if (excludeElement) {
                // offsetHeight includes padding and border
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

        // Apply the calculated height to the target element
        targetElement.style.height = `${availableHeight}px`;
        console.log(`Set element '${targetElementId}' height to ${availableHeight}px`);

    }
}

function scaleRun() {
    setElementHeightBasedOnAvailableSpace('column', 'board-section', ['nav-menu', 'board-header'])
}

function scaleInitialize() {
    scaleRun()

    window.addEventListener('resize', () => {
        scaleRun()
    })
}