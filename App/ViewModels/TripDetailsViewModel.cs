namespace RideReady.ViewModels
{
    /// <summary>
    /// The booking details shown on a dashboard / driver-portal card, as labelled rows. The driver version is
    /// built by <see cref="ForDriver"/> from a view-model that has no fare, payment or email at all, so none of
    /// those can reach the driver's screen.
    /// </summary>
    public class TripDetailsViewModel
    {
        public string CustomerName { get; init; } = string.Empty;
        public string CustomerPhone { get; init; } = string.Empty;
        public string? CustomerEmail { get; init; }
        public string? FromName { get; init; }
        public string? ToName { get; init; }
        public string? PickupLocation { get; init; }
        public string? Destination { get; init; }
        public DateOnly PickupDate { get; init; }
        public TimeOnly PickupTime { get; init; }
        public DateOnly? DropOffDate { get; init; }
        public TimeOnly? DropOffTime { get; init; }
        public int Passengers { get; init; }
        public int Bags { get; init; }
        public string? VehicleType { get; init; }
        public string? Remarks { get; init; }

        /// <summary>Admin only. Null means "don't show a payment row".</summary>
        public string? PaymentMethod { get; init; }

        public static TripDetailsViewModel ForAdmin(AdminBookingListItemViewModel booking) => new()
        {
            CustomerName = booking.CustomerName,
            CustomerPhone = booking.CustomerPhone,
            CustomerEmail = booking.CustomerEmail,
            FromName = booking.FromName,
            ToName = booking.ToName,
            PickupLocation = booking.PickupLocation,
            Destination = booking.Destination,
            PickupDate = booking.PickupDate,
            PickupTime = booking.PickupTime,
            DropOffDate = booking.DropOffDate,
            DropOffTime = booking.DropOffTime,
            Passengers = booking.Passengers,
            Bags = booking.Bags,
            VehicleType = booking.RequestedVehicleType,
            Remarks = booking.Notes,
            PaymentMethod = booking.PaymentMethod
        };

        /// <summary>Everything except the price, how the customer pays, and the customer's email.</summary>
        public static TripDetailsViewModel ForDriver(DriverAssignmentListItemViewModel trip) => new()
        {
            CustomerName = trip.CustomerName,
            CustomerPhone = trip.CustomerPhone,
            FromName = trip.FromName,
            ToName = trip.ToName,
            PickupLocation = trip.PickupLocation,
            Destination = trip.Destination,
            PickupDate = trip.PickupDate,
            PickupTime = trip.PickupTime,
            DropOffDate = trip.DropOffDate,
            DropOffTime = trip.DropOffTime,
            Passengers = trip.Passengers,
            Bags = trip.Bags,
            VehicleType = trip.RequestedVehicleType,
            Remarks = trip.Notes
        };

        /// <summary>The label/value pairs to display, in order. Rows with nothing to show are omitted.</summary>
        public IReadOnlyList<(string Label, string Value)> Rows()
        {
            var rows = new List<(string, string)>();

            var customer = $"{CustomerName} · {CustomerPhone}";
            if (!string.IsNullOrWhiteSpace(CustomerEmail)) customer += $" · {CustomerEmail}";
            rows.Add(("Customer", customer));

            rows.Add(("Route", TripText.Route(FromName, PickupLocation, ToName, Destination)));

            var pickup = $"{PickupDate:yyyy-MM-dd} {PickupTime:HH:mm}";
            if (!string.IsNullOrWhiteSpace(PickupLocation)) pickup += $" · {PickupLocation.Trim()}";
            rows.Add(("Pick up", pickup));

            var dropOffAt = TripText.DropOffAt(DropOffDate, DropOffTime);
            var dropOff = dropOffAt;
            if (!string.IsNullOrWhiteSpace(Destination))
                dropOff = dropOff.Length > 0 ? $"{dropOff} · {Destination.Trim()}" : Destination.Trim();
            if (dropOff.Length > 0) rows.Add(("Drop-off (approx.)", dropOff));

            var size = $"{Passengers} pax · {Bags} bags";
            if (!string.IsNullOrWhiteSpace(VehicleType)) size += $" · {VehicleType}";
            rows.Add(("Passengers & vehicle", size));

            rows.Add(("Remarks", string.IsNullOrWhiteSpace(Remarks) ? "-" : Remarks.Trim()));

            if (!string.IsNullOrWhiteSpace(PaymentMethod))
                rows.Add(("Payment", TripText.PaymentMethod(PaymentMethod)));

            return rows;
        }
    }
}
