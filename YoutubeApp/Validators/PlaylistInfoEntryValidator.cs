using FluentValidation;
using YoutubeApp.Media;

namespace YoutubeApp.Validators;

public class PlaylistInfoEntryValidator : AbstractValidator<PlaylistInfoEntry>
{
    public PlaylistInfoEntryValidator()
    {
        RuleFor(x => x.id).NotEmpty();
        RuleFor(x => x.title).NotEmpty();
        RuleFor(x => x.thumbnails).NotNull()
            .ForEach(rule => rule.ChildRules(th => th.RuleFor(x => x.url).NotEmpty()));
    }
}