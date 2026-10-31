// Scrolls back to the top of the page when the element is not fully visible, e.g. on a phone
// where the map pushes the controls below the flag.
export function scrollToTopUnlessVisible(element) {
    const rect = element.getBoundingClientRect();
    if (rect.top < 0 || rect.bottom > window.innerHeight) {
        window.scrollTo({ top: 0, behavior: "smooth" });
    }
}
