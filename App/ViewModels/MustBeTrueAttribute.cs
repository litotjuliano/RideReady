using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RideReady.ViewModels
{
    /// <summary>
    /// Requires a bool to be true (e.g. an "accept terms" checkbox). Validates on the server and emits
    /// data-val-mustbetrue so the matching client rule in the booking form can check the checkbox.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
    {
        public override bool IsValid(object? value) => value is true;

        public void AddValidation(ClientModelValidationContext context)
        {
            context.Attributes["data-val"] = "true";
            context.Attributes["data-val-mustbetrue"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
        }
    }
}
