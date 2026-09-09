using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HerYerde.Web.Infrastructure;

/// <summary>
/// Arayüz kültürü tr-TR olsa da ondalık alanlar HTML number input'uyla aynı biçimde
/// (1290.50) okunur; "1290,50" beklenmez.
/// </summary>
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    /// <summary>Binlik ayracı kabul edilmez: "1290,50" sessizce 129050 olmasın.</summary>
    private const NumberStyles DecimalStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var raw = value.FirstValue;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (decimal.TryParse(raw, DecimalStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Geçerli bir sayı girin (örnek: 1290.50).");
        }

        return Task.CompletedTask;
    }
}

public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.UnderlyingOrModelType == typeof(decimal)
            ? new InvariantDecimalModelBinder()
            : null;
    }
}
