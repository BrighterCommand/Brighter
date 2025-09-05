namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public class AddAlbum(string title, string artist) : Command(Brighter.Id.Random())
{
    public string Title { get; set; } = title;
    public string Artist { get; set; } = artist;
}
