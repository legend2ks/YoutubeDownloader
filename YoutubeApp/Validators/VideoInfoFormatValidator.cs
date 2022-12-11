using FluentValidation;
using YoutubeApp.Media;

namespace YoutubeApp.Validators;

public class VideoInfoFormatValidator : AbstractValidator<VideoInfoFormat>
{
    public VideoInfoFormatValidator()
    {
        RuleFor(x => x.format_id).NotEmpty();
        RuleFor(x => x.url).NotEmpty();
        RuleFor(x => x.protocol).NotEmpty();
    }
}