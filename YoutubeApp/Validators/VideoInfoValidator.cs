using FluentValidation;
using YoutubeApp.Media;

namespace YoutubeApp.Validators;

public class VideoInfoValidator : AbstractValidator<VideoInfo>
{
    public VideoInfoValidator()
    {
        RuleFor(x => x.title).NotEmpty();
        RuleFor(x => x.duration_string).NotEmpty();
        RuleFor(x => x.channel).NotEmpty();
        RuleFor(x => x.upload_date).NotNull().Matches(@"^\d{8}$");
        RuleFor(x => x.live_status).NotEmpty();
        RuleFor(x => x.formats).NotNull()
            .ForEach(x => x.SetValidator(new VideoInfoFormatValidator()));
    }
}