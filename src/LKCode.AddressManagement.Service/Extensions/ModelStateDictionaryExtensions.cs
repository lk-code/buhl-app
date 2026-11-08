using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LKCode.AddressManagement.Service.Extensions;

/// <summary>
/// Transfers validation results into a <see cref="ModelStateDictionary"/>.
/// </summary>
public static class ModelStateDictionaryExtensions
{
    /// <summary>
    /// Adds all errors of the <paramref name="validationResult"/> to the <paramref name="modelState"/>.
    /// </summary>
    /// <param name="modelState">The model state to add the errors to.</param>
    /// <param name="validationResult">The result of a validation.</param>
    /// <param name="namingPolicy">
    /// The policy that converts the property names into the names used in JSON, e.g. <c>FirstName</c> into
    /// <c>firstName</c>; <see langword="null"/> to keep the property names.
    /// </param>
    /// <remarks>
    /// Each error is added under the converted name of its property, so that a client can relate it to the field of
    /// the request body. An error without a property name is added under the empty key. Existing errors are kept.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modelState"/> or <paramref name="validationResult"/> is <see langword="null"/>.
    /// </exception>
    public static void AddValidationErrors(
        this ModelStateDictionary modelState,
        ValidationResult validationResult,
        JsonNamingPolicy? namingPolicy)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(validationResult);

        foreach (ValidationFailure failure in validationResult.Errors)
        {
            modelState.AddModelError(ToKey(failure.PropertyName, namingPolicy), failure.ErrorMessage);
        }
    }

    private static string ToKey(string? propertyName, JsonNamingPolicy? namingPolicy) =>
        namingPolicy is null || string.IsNullOrEmpty(propertyName)
            ? propertyName ?? string.Empty
            : namingPolicy.ConvertName(propertyName);
}
