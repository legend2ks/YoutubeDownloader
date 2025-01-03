using FluentValidation;
using YoutubeApp.Media;

namespace YoutubeApp.Validators;

public class PlaylistInfoValidator : AbstractValidator<PlaylistInfo>
{
    public PlaylistInfoValidator()
    {
        RuleFor(x => x.id).NotEmpty();
        RuleFor(x => x.title).NotEmpty();
        RuleFor(x => x.channel).NotEmpty();
        RuleFor(x => x.uploader).NotEmpty();
        RuleFor(x => x.entries).NotNull()
            .ForEach(x => x.SetValidator(new PlaylistInfoEntryValidator()));
    }
}