// Light / dark switch. By default RideReady follows the device setting; the button lets a person choose, and the choice
// is remembered on this device only. Loaded in <head> so the saved choice is applied before the page paints.
(function () {
    "use strict";
    var KEY = "rideready.theme";
    var root = document.documentElement;

    function saved() {
        try {
            var value = window.localStorage.getItem(KEY);
            return value === "light" || value === "dark" ? value : null;
        } catch (e) { return null; }
    }

    function remember(value) {
        try { window.localStorage.setItem(KEY, value); } catch (e) { /* storage blocked: the choice lasts for this visit only */ }
    }

    function deviceIsDark() {
        return !!(window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
    }

    function current() {
        return root.getAttribute("data-theme") || (deviceIsDark() ? "dark" : "light");
    }

    var choice = saved();
    if (choice) { root.setAttribute("data-theme", choice); }

    function sync(button) {
        var dark = current() === "dark";
        button.setAttribute("aria-label", dark ? "Switch to light mode" : "Switch to dark mode");
        button.setAttribute("title", dark ? "Switch to light mode" : "Switch to dark mode");
        var sun = button.querySelector("[data-icon-sun]");
        var moon = button.querySelector("[data-icon-moon]");
        if (sun) { sun.toggleAttribute("hidden", !dark); }
        if (moon) { moon.toggleAttribute("hidden", dark); }
    }

    document.addEventListener("DOMContentLoaded", function () {
        var buttons = Array.prototype.slice.call(document.querySelectorAll("[data-theme-toggle]"));
        buttons.forEach(function (button) {
            sync(button);
            button.addEventListener("click", function () {
                var next = current() === "dark" ? "light" : "dark";
                root.setAttribute("data-theme", next);
                remember(next);
                buttons.forEach(sync);
            });
        });
    });
})();
