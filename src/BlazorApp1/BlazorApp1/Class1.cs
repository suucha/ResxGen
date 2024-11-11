using Microsoft.Extensions.Localization;

namespace BlazorApp1
{
    public class Class1(IStringLocalizer<Class1> stringLocalizer)
    {
        public void Test()
        {
            var text = stringLocalizer["Test"];
        }
    }
}
