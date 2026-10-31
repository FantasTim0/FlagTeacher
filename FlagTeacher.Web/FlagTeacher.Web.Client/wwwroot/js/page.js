// Scrolls back to the top of the page when the element is not fully visible, e.g. on a phone
// where the map pushes the controls below the flag.
//
// On iOS the on-screen keyboard is usually still open when a guess is submitted. It closes when the picker
// is disabled, and that cancels a scroll already running, so the keyboard is closed first and the scroll
// waits until it is gone. Returns right away so the result shows without waiting for the scroll.
export function scrollToTopUnlessVisible(element) {
    const keyboardOpen = isKeyboardOpen();
    document.activeElement?.blur?.();
    (keyboardOpen ? keyboardClosed() : Promise.resolve()).then(() => {
        const rect = element.getBoundingClientRect();
        if (rect.top < 0 || rect.bottom > window.innerHeight) {
            window.scrollTo({ top: 0, behavior: "smooth" });
        }
    });
}

// Closes the on-screen keyboard by taking the focus off the picker. Without one (on a desktop) the focus
// stays, so return can still submit the guess right away. A touch screen counts as having one, since the
// viewport does not always show whether it is open.
export function closeKeyboard() {
    if (isKeyboardOpen() || window.matchMedia("(pointer: coarse)").matches) {
        document.activeElement?.blur?.();
    }
}

// The visual viewport shrinks by the height of the on-screen keyboard.
function isKeyboardOpen() {
    return window.visualViewport !== undefined && window.visualViewport.height < window.innerHeight - 100;
}

function keyboardClosed() {
    return new Promise(resolve => {
        const started = performance.now();
        const check = () => {
            if (!isKeyboardOpen() || performance.now() - started > 1000) {
                // Give the viewport a moment to settle after the keyboard animation.
                setTimeout(resolve, 100);
            } else {
                requestAnimationFrame(check);
            }
        };
        requestAnimationFrame(check);
    });
}
