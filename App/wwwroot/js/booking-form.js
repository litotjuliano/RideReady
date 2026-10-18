// Booking form extras: a live trip summary and "remember me on this device".
// Nothing here changes what is submitted; the server still validates everything.
(function () {
    "use strict";

    var form = document.querySelector("form[action$='/Booking/Create'], form[action$='/Create']") || document.querySelector(".rr-card form");
    if (!form) { return; }

    function field(id) { return document.getElementById(id); }
    function value(id) { var el = field(id); return el ? (el.value || "").trim() : ""; }

    // ---------- Live trip summary ----------
    var summary = field("rr-trip-summary");
    var list = summary ? summary.querySelector("dl") : null;

    function formatDate(iso) {
        var match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
        if (!match) { return ""; }
        var date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
        return date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
    }

    function formatTime(hhmm) {
        var match = /^(\d{1,2}):(\d{2})/.exec(hhmm);
        if (!match) { return ""; }
        var hours = Number(match[1]);
        var suffix = hours >= 12 ? "PM" : "AM";
        return (hours % 12 || 12) + ":" + match[2] + " " + suffix;
    }

    function join(parts, separator) {
        return parts.filter(function (p) { return p; }).join(separator);
    }

    function selectedVehicle() {
        var radio = form.querySelector('input[name="VehicleType"]:checked');
        if (radio && radio.value === "Others") {
            var other = value("OtherVehicle");
            return other ? "Others: " + other : "Others";
        }
        return radio ? radio.value : "";
    }

    function plural(count, word) {
        return count + " " + word + (count === 1 ? "" : "s");
    }

    function rows() {
        var passengers = Number(value("Passengers"));
        var bags = value("Bags") === "" ? NaN : Number(value("Bags"));
        var who = join([
            passengers > 0 ? plural(passengers, "passenger") : "",
            !isNaN(bags) ? plural(bags, "bag") : "",
            selectedVehicle()
        ], " \u00b7 ");

        return [
            ["Route", join([value("FromName"), value("ToName")], " \u2192 ")],
            ["Pick up", join([join([formatDate(value("PickupDate")), formatTime(value("PickupTime"))], ", "), value("PickupLocation")], " \u00b7 ")],
            ["Drop-off (approx.)", join([join([formatDate(value("DropOffDate")), formatTime(value("DropOffTime"))], ", "), value("Destination")], " \u00b7 ")],
            ["Passengers & vehicle", who]
        ].filter(function (row) { return row[1]; });
    }

    // Wait until the customer has started on the trip itself, so the defaults alone do not show a summary.
    function hasTripText() {
        return !!(value("FromName") || value("ToName") || value("PickupLocation") || value("Destination"));
    }

    function renderSummary() {
        if (!summary || !list) { return; }
        var shown = hasTripText() ? rows() : [];
        while (list.firstChild) { list.removeChild(list.firstChild); }
        shown.forEach(function (row) {
            var dt = document.createElement("dt");
            dt.textContent = row[0];
            var dd = document.createElement("dd");
            dd.textContent = row[1];
            list.appendChild(dt);
            list.appendChild(dd);
        });
        summary.hidden = shown.length === 0;
    }

    form.addEventListener("input", renderSummary);
    form.addEventListener("change", renderSummary);

    // ---------- Remember me on this device ----------
    var KEY = "rideready.customer";
    var bar = field("rr-welcome");
    var nameEl = field("CustomerName");
    var phoneEl = field("CustomerPhone");
    var emailEl = field("CustomerEmail");

    function load() {
        try {
            var raw = window.localStorage.getItem(KEY);
            var saved = raw ? JSON.parse(raw) : null;
            return saved && saved.name ? saved : null;
        } catch (e) { return null; }
    }

    function save() {
        try {
            window.localStorage.setItem(KEY, JSON.stringify({ name: value("CustomerName"), phone: value("CustomerPhone"), email: value("CustomerEmail") }));
        } catch (e) { /* storage blocked: skip */ }
    }

    function forget() {
        try { window.localStorage.removeItem(KEY); } catch (e) { /* ignore */ }
    }

    function fill(target, text) {
        if (!target) { return; }
        target.value = text || "";
        target.dispatchEvent(new Event("input", { bubbles: true }));
        target.dispatchEvent(new Event("change", { bubbles: true }));
    }

    var saved = load();
    if (bar && saved && !value("CustomerName") && !value("CustomerPhone") && !value("CustomerEmail")) {
        var greeting = bar.querySelector("[data-welcome-name]");
        if (greeting) { greeting.textContent = saved.name.split(" ")[0]; }
        bar.hidden = false;

        bar.querySelector("[data-welcome-use]").addEventListener("click", function () {
            fill(nameEl, saved.name);
            fill(phoneEl, saved.phone);
            fill(emailEl, saved.email);
            bar.hidden = true;
        });
        bar.querySelector("[data-welcome-forget]").addEventListener("click", function () {
            forget();
            bar.hidden = true;
        });
    }

    // Save only when the form really goes through (client-side validation did not stop it).
    form.addEventListener("submit", function (event) {
        window.setTimeout(function () {
            if (!event.defaultPrevented && value("CustomerName")) { save(); }
        }, 0);
    });

    renderSummary();
})();
