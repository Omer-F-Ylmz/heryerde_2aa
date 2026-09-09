using System.Globalization;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace HerYerde.Tests.Web;

public sealed class InvariantDecimalModelBinderTests
{
    private static async Task<DefaultModelBindingContext> BindAsync(string raw)
    {
        var context = new DefaultModelBindingContext
        {
            ModelName = "Price",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new SimpleValueProvider(raw),
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal))
        };

        await new InvariantDecimalModelBinder().BindModelAsync(context);
        return context;
    }

    [Fact]
    public async Task Noktali_ondalik_tr_TR_kulturunde_bile_dogru_okunur()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var context = await BindAsync("1290.50");

            Assert.True(context.Result.IsModelSet);
            Assert.Equal(1290.50m, context.Result.Model);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Virgullu_ondalik_sessizce_100_katina_cikmaz_hata_verir()
    {
        var context = await BindAsync("1290,50");

        Assert.False(context.Result.IsModelSet);
        Assert.False(context.ModelState.IsValid);
    }

    private sealed class SimpleValueProvider : IValueProvider
    {
        private readonly string _value;

        public SimpleValueProvider(string value) => _value = value;

        public bool ContainsPrefix(string prefix) => true;

        public ValueProviderResult GetValue(string key)
            => new(new StringValues(_value), CultureInfo.InvariantCulture);
    }
}
