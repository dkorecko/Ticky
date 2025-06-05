// Focus trap for element
function trapFocusInElement(element) {
    if (!element) return;

    const focusableSelectors = [
        'a[href]', 'area[href]', 'input:not([disabled]):not([type="hidden"])', 'select:not([disabled])',
        'textarea:not([disabled])', 'button:not([disabled])', 'iframe', 'object', 'embed',
        '[tabindex]:not([tabindex="-1"])', '[contenteditable]'
    ];

    const visibleElements = Array.from(element.querySelectorAll(focusableSelectors.join(','))).filter(el => {
        return el.offsetWidth > 0 || el.offsetHeight > 0 || el === document.activeElement;
    });

    if (!visibleElements.length) return;

    const first = visibleElements[0];
    const last = visibleElements[visibleElements.length - 1];

    // Focus the first element only if not already focused
    setTimeout(() => {
        if (document.activeElement !== first) {
            first.focus();
        }
    }, 0);

    function handleTrap(e) {
        if (e.key === 'Tab') {
            if (e.shiftKey) {
                if (document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                }
            } else {
                if (document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            }
        }
        if (e.key === 'Escape') {
			closeDropdowns();
        }
    }
    element.addEventListener('keydown', handleTrap);
    function cleanup() {
        element.removeEventListener('keydown', handleTrap);
        element.removeEventListener('closeDropdowns', cleanup);
    }

    element.addEventListener('closeDropdowns', cleanup);
}

window.trapFocusInElement = trapFocusInElement;