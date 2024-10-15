using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorApp1.Client.Pages
{
    public partial class Counter
    {
        private int currentCount = 0;
        [Inject]
        private IStringLocalizer<Counter> stringLocalizer { get; set; }
        private void IncrementCount()
        {
            Console.WriteLine(stringLocalizer["Click button."]);
            currentCount++;
        }
    }
}