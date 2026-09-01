(function () {
    var button = document.getElementById('locationToggle');
    if (!button) {
        return;
    }

    var intervalId = null;
    var bookingId = button.dataset.bookingId || '';

    function reportPosition() {
        if (!navigator.geolocation) {
            return;
        }

        navigator.geolocation.getCurrentPosition(function (position) {
            var payload = {
                bookingId: bookingId ? parseInt(bookingId, 10) : null,
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                accuracyMeters: position.coords.accuracy ? Math.round(position.coords.accuracy) : null,
                speedKmh: position.coords.speed ? position.coords.speed * 3.6 : null
            };

            fetch('/Driver/ReportLocation', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            }).catch(function (err) {
                console.warn('Failed to report location:', err);
            });
        }, function (error) {
            console.warn('Location error:', error.message);
        });
    }

    button.addEventListener('click', function () {
        if (intervalId) {
            clearInterval(intervalId);
            intervalId = null;
            button.textContent = 'Share my location';
        } else {
            reportPosition();
            intervalId = setInterval(reportPosition, 30000);
            button.textContent = 'Stop sharing location';
        }
    });
})();
