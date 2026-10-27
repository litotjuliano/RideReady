namespace RideReady.Services
{
    public interface IWhatsAppSender
    {
        Task SendAsync(string toPhone, string message);

        /// <summary>
        /// Sends an approved template that has one variable per item in <paramref name="parameters"/>, so the template
        /// body itself can lay the message out on separate lines (a single variable cannot contain line breaks).
        /// </summary>
        Task SendTemplateAsync(string toPhone, string templateName, IReadOnlyList<string> parameters);
    }
}
