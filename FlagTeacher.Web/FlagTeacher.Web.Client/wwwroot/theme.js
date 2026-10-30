// Runs in <head> before the first paint so the page never flashes the wrong theme.
(function () {
    var root = document.documentElement;
    var media = window.matchMedia("(prefers-color-scheme: dark)");

    function stored() {
        try {
            return localStorage.getItem("theme");
        } catch (e) {
            return null;
        }
    }

    function apply(theme) {
        root.setAttribute("data-theme", theme);
        var toggle = document.getElementById("theme-toggle");
        if (toggle) {
            var label = theme === "dark" ? "Zum hellen Modus wechseln" : "Zum dunklen Modus wechseln";
            toggle.title = label;
            toggle.setAttribute("aria-label", label);
        }
    }

    apply(stored() || (media.matches ? "dark" : "light"));

    media.addEventListener("change", function (e) {
        if (!stored()) {
            apply(e.matches ? "dark" : "light");
        }
    });

    document.addEventListener("DOMContentLoaded", function () {
        apply(root.getAttribute("data-theme"));
    });

    document.addEventListener("click", function (e) {
        if (!e.target.closest("#theme-toggle")) {
            return;
        }
        var next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
        try {
            localStorage.setItem("theme", next);
        } catch (e) {
        }
        apply(next);
    });
})();
