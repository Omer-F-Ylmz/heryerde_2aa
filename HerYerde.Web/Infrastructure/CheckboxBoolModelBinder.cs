using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HerYerde.Web.Infrastructure;

/// <summary>
/// Onay kutusu "true"/"false" gönderir. Başka bir değer (ör. "zap") varsayılan bağlayıcıda ham haliyle ModelState'e yazılır ve
/// form yeniden çizilirken InputTagHelper onu bool'a çeviremeyip FormatException (500) atar; burada doğrulama hatası olur,
/// ModelState'e false yazılır.
/// </summary>
public sealed class CheckboxBoolModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        if (bool.TryParse(value.FirstValue, out var parsed))
        {
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, false, "false");
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Geçersiz seçim.");
        }

        return Task.CompletedTask;
    }
}

public sealed class CheckboxBoolModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(bool) ? new CheckboxBoolModelBinder() : null;
    }
}
