namespace Paramore.Domain.Venues
{
    public class VenueName
    {
        private string name;

        public VenueName(string venueName)
        {
            name = venueName;
        }

        public static implicit operator string(VenueName venueName)
        {
            return venueName.name;
        }
    }
}
