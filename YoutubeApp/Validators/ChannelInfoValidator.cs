using FluentValidation;
using YoutubeApp.Media;

namespace YoutubeApp.Validators;

public class ChannelInfoValidator : AbstractValidator<ChannelInfo>
{
    public ChannelInfoValidator()
    {
        RuleFor(x => x.channel_id).NotEmpty();
    }
}