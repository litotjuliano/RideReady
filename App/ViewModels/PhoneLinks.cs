using System.Text;

namespace RideReady.ViewModels
{
    /// <summary>
    /// Malaysian phone number helpers: the international form used for WhatsApp, and the links that let the admin
    /// call or message a customer or driver with one tap. One place, so WhatsApp sending and the links agree.
    /// </summary>
    public static class PhoneLinks
    {
        /// <summary>
        /// Digits only, in international form without a plus: "0123456789", "012-345 6789" and "+60123456789" all
        /// become "60123456789". Anything that doesn't start with 0 is returned as its digits unchanged.
        /// </summary>
        public static string ToInternational(string? phone)
        {
            var digits = new StringBuilder();
            foreach (var c in phone ?? string.Empty)
            {
                if (char.IsDigit(c)) digits.Append(c);
            }

            var text = digits.ToString();
            return text.StartsWith('0') ? "60" + text[1..] : text;
        }

        /// <summary>A tap-to-call link, e.g. "tel:+60123456789". Empty when there is no usable number.</summary>
        public static string Tel(string? phone)
        {
            var number = ToInternational(phone);
            return number.Length == 0 ? string.Empty : $"tel:+{number}";
        }

        /// <summary>A WhatsApp chat link, e.g. "https://wa.me/60123456789". Empty when there is no usable number.</summary>
        public static string WhatsApp(string? phone)
        {
            var number = ToInternational(phone);
            return number.Length == 0 ? string.Empty : $"https://wa.me/{number}";
        }
    }
}
