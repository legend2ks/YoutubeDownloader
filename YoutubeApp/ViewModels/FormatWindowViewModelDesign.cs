using System.Collections.Generic;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

internal class FormatWindowViewModelDesign : FormatWindowViewModel
{
    public FormatWindowViewModelDesign()
    {
        ComposedVariants = new()
        {
            new ComposedVariant(new List<CommonVariant>(), new List<Download>()),
            new ComposedVariant(new List<CommonVariant>(), new List<Download>()),
            new ComposedVariant(new List<CommonVariant>(), new List<Download>())
        };
        RemainingVideoCount = 12;
    }
}