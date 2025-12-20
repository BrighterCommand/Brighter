using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public class AddAlbumRequestHandler : RequestHandler<AddAlbum>
{
    private readonly IAmATransactionConnectionProvider _uow;
    private readonly Discography _discography;
    private readonly IAmACommandProcessor _postbox;

    public AddAlbumRequestHandler(IAmATransactionConnectionProvider uow, Discography discography, IAmACommandProcessor commandProcessor)
    {
        _uow = uow;
        _discography = discography;
        _postbox = commandProcessor;
    }
    
    public override AddAlbum Handle(AddAlbum addAlbum)
    {
        var posts = new List<Id>();
        
        var album = new Album(addAlbum.Artist, addAlbum.Title);
        var tx = _uow.GetTransaction();
        try
        {
            _discography.Albums.Add(album);
            posts.Add(_postbox.DepositPost(new AlbumAdded(album.Title, addAlbum.Id)));
            _discography.SaveChanges();
            _uow.Commit();
        }
        catch
        {
            tx.Rollback();
        }
        
        _postbox.ClearOutbox(posts.ToArray());
        return base.Handle(addAlbum);
    }
}
